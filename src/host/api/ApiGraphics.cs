using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using static RimKit.ApiHelpers;

namespace RimKit
{
    // game.graphics (textures, def graphics, colours, pawn looks, render nodes) and game.materials (materials, shaders, bundles).
    // Ops: graphics.*, material.*.
    internal static class ApiGraphics
    {
        private static readonly Dictionary<int, Material> Materials = new Dictionary<int, Material>();
        private static readonly Dictionary<string, Shader> BundleShaders = new Dictionary<string, Shader>();
        private static readonly List<AssetBundle> Bundles = new List<AssetBundle>();
        private static int nextMaterial = 1;

        public static void Register()
        {
            R("graphics.texture_info", TextureInfo);
            R("graphics.set_def_graphic", SetDefGraphic);
            R("graphics.set_color", SetColor);
            R("graphics.refresh", Refresh);
            R("graphics.body_types", BodyTypes);
            R("graphics.hairs", Hairs);
            R("graphics.head_types", HeadTypes);
            R("graphics.set_body_type", SetBodyType);
            R("graphics.set_hair", SetHair);
            R("graphics.set_hair_color", SetHairColor);
            R("graphics.set_skin_color", SetSkinColor);
            R("graphics.set_head_type", SetHeadType);
            R("graphics.add_render_node", AddRenderNode);
            R("graphics.play_animation", PlayAnimation);
            R("material.shaders", Shaders);
            R("material.create", MaterialCreate);
            R("material.set_color", MaterialSetColor);
            R("material.set_float", MaterialSetFloat);
            R("material.set_texture", MaterialSetTexture);
            R("material.info", MaterialInfo);
            R("material.destroy", MaterialDestroy);
            R("material.load_bundle", LoadBundle);
        }

        private static void R(string op, Func<Dictionary<string, string>, string> fn) => ApiRegistry.Register(op, a => fn(a), "gameplay", "0.8.0");

        internal static Material MaterialById(int id) => Materials.TryGetValue(id, out Material m) ? m : null;

        private static bool ParseColor(string spec, out Color c)
        {
            c = Color.white;
            if (string.IsNullOrEmpty(spec)) return false;
            if (WidgetRenderer.ColorOf(spec, new Color(-1f, 0f, 0f)).r >= 0f) { c = WidgetRenderer.ColorOf(spec, Color.white); return true; }
            return ColorUtility.TryParseHtmlString(spec, out c);
        }

        private static Shader ShaderByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (BundleShaders.TryGetValue(name, out Shader s)) return s;
            FieldInfo f = typeof(ShaderDatabase).GetField(name, BindingFlags.Public | BindingFlags.Static);
            if (f != null && f.FieldType == typeof(Shader)) return (Shader)f.GetValue(null);
            return Shader.Find(name);
        }

        private static string Bad(string what) => Fail("RK2001", what + " handle is stale or null");

        // ---- graphics

        private static string TextureInfo(Dictionary<string, string> a)
        {
            Texture2D t = ContentFinder<Texture2D>.Get(Str(a, "path"), false);
            return t == null ? Jb.Obj().B("exists", false).Ok() : Jb.Obj().B("exists", true).I("width", t.width).I("height", t.height).Ok();
        }

        // opts: shader (Cutout, CutoutComplex, Transparent, ... or a bundle shader), color, width, height (draw size).
        private static string SetDefGraphic(Dictionary<string, string> a)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(Str(a, "def"));
            if (def == null) return Fail("RK3001", "unknown thing def " + Str(a, "def"));
            string tex = Str(a, "texture");
            if (ContentFinder<Texture2D>.Get(tex, false) == null && ContentFinder<Texture2D>.Get(tex + "_south", false) == null) return Fail("RK3001", "texture not found: " + tex);
            Opts o = Opts.From(a, "opts");
            if (def.graphicData == null) def.graphicData = new GraphicData { graphicClass = typeof(Graphic_Single) };
            GraphicData gd = def.graphicData;
            gd.texPath = tex;
            if (o.Has("width") || o.Has("height")) gd.drawSize = new Vector2((float)o.Num("width", gd.drawSize.x), (float)o.Num("height", gd.drawSize.y));
            if (ParseColor(o.Str("color"), out Color c)) gd.color = c;
            if (!string.IsNullOrEmpty(o.Str("shader")))
            {
                Shader sh = ShaderByName(o.Str("shader"));
                if (sh == null) return Fail("RK3001", "unknown shader " + o.Str("shader"));
                var typeDef = new ShaderTypeDef { defName = "RimKit_Shader_" + o.Str("shader"), shaderPath = o.Str("shader") };
                AccessTools.Field(typeof(ShaderTypeDef), "shaderInt")?.SetValue(typeDef, sh);
                gd.shaderType = typeDef;
            }

            AccessTools.Field(typeof(GraphicData), "cachedGraphic")?.SetValue(gd, null);
            def.graphic = gd.Graphic;
            foreach (Map map in Find.Maps)
                foreach (Thing t in map.listerThings.ThingsOfDef(def)) t.DirtyMapMesh(map);
            return OkBool(true);
        }

        private static string SetColor(Dictionary<string, string> a)
        {
            Thing t = ThingOf(a);
            if (t == null) return Bad("thing");
            if (!ParseColor(Str(a, "color"), out Color c)) return Fail("RK1001", "color must look like #rrggbb or be a theme color");
            t.SetColor(c, true);
            return OkBool(true);
        }

        private static string Refresh(Dictionary<string, string> a)
        {
            Thing t = ThingOf(a);
            if (t == null) return Bad("thing");
            if (t is Pawn p) p.Drawer?.renderer?.SetAllGraphicsDirty();
            else if (t.Spawned) t.DirtyMapMesh(t.Map);
            return OkBool(true);
        }

        private static string BodyTypes(Dictionary<string, string> a) => OkStringList(DefDatabase<BodyTypeDef>.AllDefsListForReading.Select(d => d.defName).ToList());

        private static string Hairs(Dictionary<string, string> a) => OkStringList(DefDatabase<HairDef>.AllDefsListForReading.Select(d => d.defName).ToList());

        private static string HeadTypes(Dictionary<string, string> a) => OkStringList(DefDatabase<HeadTypeDef>.AllDefsListForReading.Select(d => d.defName).ToList());

        private static Pawn Styled(Dictionary<string, string> a, out string err)
        {
            err = null;
            Pawn p = PawnOf(a);
            if (p == null) { err = Bad("pawn"); return null; }
            if (p.story == null) { err = Fail("RK3003", "this pawn has no story data"); return null; }
            return p;
        }

        private static string Redraw(Pawn p)
        {
            p.Drawer?.renderer?.SetAllGraphicsDirty();
            PortraitsCache.SetDirty(p);
            GlobalTextureAtlasManager.TryMarkPawnFrameSetDirty(p);
            return OkBool(true);
        }

        private static string SetBodyType(Dictionary<string, string> a)
        {
            Pawn p = Styled(a, out string err);
            if (err != null) return err;
            BodyTypeDef d = DefDatabase<BodyTypeDef>.GetNamedSilentFail(Str(a, "def"));
            if (d == null) return Fail("RK3001", "unknown body type " + Str(a, "def"));
            p.story.bodyType = d;
            return Redraw(p);
        }

        private static string SetHair(Dictionary<string, string> a)
        {
            Pawn p = Styled(a, out string err);
            if (err != null) return err;
            HairDef d = DefDatabase<HairDef>.GetNamedSilentFail(Str(a, "def"));
            if (d == null) return Fail("RK3001", "unknown hair " + Str(a, "def"));
            p.story.hairDef = d;
            return Redraw(p);
        }

        private static string SetHairColor(Dictionary<string, string> a)
        {
            Pawn p = Styled(a, out string err);
            if (err != null) return err;
            if (!ParseColor(Str(a, "color"), out Color c)) return Fail("RK1001", "color must look like #rrggbb");
            p.story.HairColor = c;
            return Redraw(p);
        }

        private static string SetSkinColor(Dictionary<string, string> a)
        {
            Pawn p = Styled(a, out string err);
            if (err != null) return err;
            if (!ParseColor(Str(a, "color"), out Color c)) return Fail("RK1001", "color must look like #rrggbb");
            p.story.skinColorOverride = c;
            return Redraw(p);
        }

        private static string SetHeadType(Dictionary<string, string> a)
        {
            Pawn p = Styled(a, out string err);
            if (err != null) return err;
            HeadTypeDef d = DefDatabase<HeadTypeDef>.GetNamedSilentFail(Str(a, "def"));
            if (d == null) return Fail("RK3001", "unknown head type " + Str(a, "def"));
            p.story.headType = d;
            return Redraw(p);
        }

        // Adds a texture as a render node on every pawn of a race (the 1.6 render tree). opts: layer, parent (Body, Head), color, width, height.
        // Pawns pick it up the next time their graphics are rebuilt, which this call triggers for pawns on maps.
        private static string AddRenderNode(Dictionary<string, string> a)
        {
            ThingDef race = DefDatabase<ThingDef>.GetNamedSilentFail(Str(a, "race"));
            if (race?.race?.renderTree == null) return Fail("RK3001", Str(a, "race") + " is not a race with a render tree");
            string tex = Str(a, "texture");
            if (ContentFinder<Texture2D>.Get(tex, false) == null && ContentFinder<Texture2D>.Get(tex + "_south", false) == null) return Fail("RK3001", "texture not found: " + tex);
            Opts o = Opts.From(a, "opts");
            var props = new PawnRenderNodeProperties
            {
                debugLabel = "RimKit_" + Str(a, "id"),
                texPath = tex,
                nodeClass = typeof(PawnRenderNode),
                workerClass = typeof(PawnRenderNodeWorker),
                parentTagDef = o.Str("parent", "Body") == "Head" ? PawnRenderNodeTagDefOf.Head : PawnRenderNodeTagDefOf.Body,
                baseLayer = (float)o.Num("layer", 50),
                drawSize = new Vector2((float)o.Num("width", 1), (float)o.Num("height", 1)),
            };
            if (ParseColor(o.Str("color"), out Color c)) { props.color = c; props.colorType = PawnRenderNodeProperties.AttachmentColorType.Custom; }
            var root = race.race.renderTree.root;
            if (root.children == null) root.children = new List<PawnRenderNodeProperties>();
            if (root.children.Any(x => x.debugLabel == props.debugLabel)) return OkBool(false);
            root.children.Add(props);
            foreach (Map map in Find.Maps)
                foreach (Pawn p in map.mapPawns.AllPawns.Where(x => x.def == race)) p.Drawer?.renderer?.SetAllGraphicsDirty();
            return OkBool(true);
        }

        private static string PlayAnimation(Dictionary<string, string> a)
        {
            Pawn p = PawnOf(a);
            if (p == null) return Bad("pawn");
            AnimationDef anim = DefDatabase<AnimationDef>.GetNamedSilentFail(Str(a, "def"));
            if (anim == null) return Fail("RK3001", "unknown animation " + Str(a, "def"));
            var renderer = p.Drawer?.renderer;
            var method = AccessTools.Method(renderer?.GetType(), "SetAnimation");
            if (method == null) return Fail("RK3003", "pawn animations are not available on this game version");
            method.Invoke(renderer, new object[] { anim });
            return OkBool(true);
        }

        // ---- materials

        private static string Shaders(Dictionary<string, string> a)
        {
            var names = typeof(ShaderDatabase).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.FieldType == typeof(Shader)).Select(f => f.Name).ToList();
            names.AddRange(BundleShaders.Keys);
            return OkStringList(names.OrderBy(n => n).ToList());
        }

        // opts: color, texture (a texture path), main_texture is the default slot.
        private static string MaterialCreate(Dictionary<string, string> a)
        {
            Shader sh = ShaderByName(Str(a, "shader"));
            if (sh == null) return Fail("RK3001", "unknown shader " + Str(a, "shader"));
            var mat = new Material(sh);
            Opts o = Opts.From(a, "opts");
            if (ParseColor(o.Str("color"), out Color c)) mat.color = c;
            if (!string.IsNullOrEmpty(o.Str("texture")))
            {
                Texture2D t = ContentFinder<Texture2D>.Get(o.Str("texture"), false);
                if (t == null) return Fail("RK3001", "texture not found: " + o.Str("texture"));
                mat.mainTexture = t;
            }

            int id = nextMaterial++;
            Materials[id] = mat;
            return OkInt(id);
        }

        private static Material Mat(Dictionary<string, string> a, out string err)
        {
            err = null;
            Material m = MaterialById(Int(a, "id"));
            if (m == null) err = Fail("RK2001", "material " + Str(a, "id") + " does not exist");
            return m;
        }

        private static string MaterialSetColor(Dictionary<string, string> a)
        {
            Material m = Mat(a, out string err);
            if (err != null) return err;
            if (!ParseColor(Str(a, "color"), out Color c)) return Fail("RK1001", "color must look like #rrggbb");
            string prop = string.IsNullOrEmpty(Str(a, "property")) ? "_Color" : Str(a, "property");
            if (!m.HasProperty(prop)) return Fail("RK3001", "the shader has no property " + prop);
            m.SetColor(prop, c);
            return OkBool(true);
        }

        private static string MaterialSetFloat(Dictionary<string, string> a)
        {
            Material m = Mat(a, out string err);
            if (err != null) return err;
            if (!m.HasProperty(Str(a, "property"))) return Fail("RK3001", "the shader has no property " + Str(a, "property"));
            m.SetFloat(Str(a, "property"), Float(a, "value"));
            return OkBool(true);
        }

        private static string MaterialSetTexture(Dictionary<string, string> a)
        {
            Material m = Mat(a, out string err);
            if (err != null) return err;
            Texture2D t = ContentFinder<Texture2D>.Get(Str(a, "path"), false);
            if (t == null) return Fail("RK3001", "texture not found: " + Str(a, "path"));
            string prop = string.IsNullOrEmpty(Str(a, "property")) ? "_MainTex" : Str(a, "property");
            if (!m.HasProperty(prop)) return Fail("RK3001", "the shader has no property " + prop);
            m.SetTexture(prop, t);
            return OkBool(true);
        }

        private static string MaterialInfo(Dictionary<string, string> a)
        {
            Material m = Mat(a, out string err);
            if (err != null) return err;
            var c = m.color;
            return Jb.Obj().S("shader", m.shader.name).S("color", "#" + ColorUtility.ToHtmlStringRGBA(c)).B("has_texture", m.mainTexture != null).I("render_queue", m.renderQueue).Ok();
        }

        private static string MaterialDestroy(Dictionary<string, string> a)
        {
            Material m = Mat(a, out string err);
            if (err != null) return err;
            Materials.Remove(Int(a, "id"));
            UnityEngine.Object.Destroy(m);
            return OkBool(true);
        }

        // Loads an asset bundle from a mod folder and registers its shaders by name. path is relative to the mod root.
        private static string LoadBundle(Dictionary<string, string> a)
        {
            ModContentPack pack = LoadedModManager.RunningModsListForReading.FirstOrDefault(m => string.Equals(m.PackageId, Str(a, "package_id"), StringComparison.OrdinalIgnoreCase));
            if (pack?.RootDir == null) return Fail("RK3001", "mod not found: " + Str(a, "package_id"));
            string root = Path.GetFullPath(pack.RootDir);
            string full = Path.GetFullPath(Path.Combine(root, Str(a, "path")));
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return Fail("RK3003", "the bundle must be inside the mod folder");
            if (!File.Exists(full)) return Fail("RK3001", "bundle not found: " + Str(a, "path"));
            AssetBundle bundle = AssetBundle.LoadFromFile(full);
            if (bundle == null) return Fail("RK3003", "the file is not a valid asset bundle for this Unity version");
            Bundles.Add(bundle);
            var names = new List<string>();
            foreach (Shader s in bundle.LoadAllAssets<Shader>())
            {
                BundleShaders[s.name] = s;
                names.Add(s.name);
            }

            return OkStringList(names);
        }
    }
}
