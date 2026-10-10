// rimkit mod assets, i18n, content checks, release checks and the new mod scaffold.
#include "tools.hpp"

#include "aliases.hpp"
#include "json_lite.hpp"

#include <algorithm>
#include <cctype>
#include <cstdint>
#include <cstdlib>
#include <cstring>
#include <ctime>
#include <fstream>
#include <iostream>
#include <regex>
#include <set>
#include <sstream>

namespace rkcli {

namespace {

std::string read_all(const fs::path& p) {
    std::ifstream f(p, std::ios::binary);
    std::stringstream ss;
    ss << f.rdbuf();
    return ss.str();
}

bool has_flag(const std::vector<std::string>& args, const std::string& name) {
    return std::find(args.begin(), args.end(), name) != args.end();
}

struct Report {
    int errors = 0;
    int warnings = 0;
    bool quiet = false;
    void error(const std::string& m) {
        ++errors;
        if (!quiet) std::cout << "  [fail] " << m << "\n";
    }
    void warn(const std::string& m) {
        ++warnings;
        if (!quiet) std::cout << "  [warn] " << m << "\n";
    }
    void ok(const std::string& m) {
        if (!quiet) std::cout << "  [ ok ] " << m << "\n";
    }
};

// ---- PNG

struct PngInfo {
    bool valid = false;
    uint32_t w = 0, h = 0;
    uintmax_t bytes = 0;
};

PngInfo png_info(const fs::path& p) {
    PngInfo info;
    std::error_code ec;
    info.bytes = fs::file_size(p, ec);
    std::ifstream f(p, std::ios::binary);
    unsigned char head[24] = {0};
    f.read(reinterpret_cast<char*>(head), 24);
    static const unsigned char sig[8] = {0x89, 'P', 'N', 'G', 0x0D, 0x0A, 0x1A, 0x0A};
    if (f.gcount() < 24 || std::memcmp(head, sig, 8) != 0 || std::memcmp(head + 12, "IHDR", 4) != 0) {
        return info;
    }
    info.w = (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19];
    info.h = (head[20] << 24) | (head[21] << 16) | (head[22] << 8) | head[23];
    info.valid = info.w > 0 && info.h > 0;
    return info;
}

uint32_t crc32_of(const unsigned char* data, size_t n, uint32_t crc = 0xFFFFFFFFu) {
    static uint32_t table[256];
    static bool init = false;
    if (!init) {
        for (uint32_t i = 0; i < 256; ++i) {
            uint32_t c = i;
            for (int k = 0; k < 8; ++k) c = (c & 1) ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[i] = c;
        }
        init = true;
    }
    for (size_t i = 0; i < n; ++i) crc = table[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
    return crc;
}

void put32(std::string& s, uint32_t v) {
    s += static_cast<char>(v >> 24);
    s += static_cast<char>(v >> 16);
    s += static_cast<char>(v >> 8);
    s += static_cast<char>(v);
}

void chunk(std::string& out, const char* type, const std::string& data) {
    put32(out, static_cast<uint32_t>(data.size()));
    std::string body = std::string(type, 4) + data;
    out += body;
    put32(out, crc32_of(reinterpret_cast<const unsigned char*>(body.data()), body.size()) ^ 0xFFFFFFFFu);
}

// A plain coloured PNG (stored deflate blocks, no compression library needed) with a lighter diagonal band, so a placeholder is never blank.
std::string make_png(uint32_t w, uint32_t h, unsigned char r, unsigned char g, unsigned char b) {
    std::string raw;
    for (uint32_t y = 0; y < h; ++y) {
        raw += '\0';
        for (uint32_t x = 0; x < w; ++x) {
            const bool band = ((x + y) / 24) % 5 == 0;
            raw += static_cast<char>(band ? std::min(255, r + 30) : r);
            raw += static_cast<char>(band ? std::min(255, g + 30) : g);
            raw += static_cast<char>(band ? std::min(255, b + 30) : b);
        }
    }
    std::string z = "\x78\x01";
    size_t pos = 0;
    while (pos < raw.size()) {
        const size_t n = std::min<size_t>(65535, raw.size() - pos);
        z += static_cast<char>(pos + n >= raw.size() ? 1 : 0);
        z += static_cast<char>(n & 0xFF);
        z += static_cast<char>(n >> 8);
        z += static_cast<char>(~n & 0xFF);
        z += static_cast<char>((~n >> 8) & 0xFF);
        z.append(raw, pos, n);
        pos += n;
    }
    uint32_t a = 1, bsum = 0;
    for (unsigned char c : raw) {
        a = (a + c) % 65521;
        bsum = (bsum + a) % 65521;
    }
    put32(z, (bsum << 16) | a);
    std::string out = std::string("\x89PNG\r\n\x1a\n", 8);
    std::string ihdr;
    put32(ihdr, w);
    put32(ihdr, h);
    ihdr += std::string("\x08\x02\x00\x00\x00", 5);
    chunk(out, "IHDR", ihdr);
    chunk(out, "IDAT", z);
    chunk(out, "IEND", "");
    return out;
}

void colour_for(const std::string& id, unsigned char& r, unsigned char& g, unsigned char& b) {
    uint32_t h = 2166136261u;
    for (char c : id) h = (h ^ static_cast<unsigned char>(c)) * 16777619u;
    r = static_cast<unsigned char>(40 + (h & 0x7F));
    g = static_cast<unsigned char>(40 + ((h >> 8) & 0x7F));
    b = static_cast<unsigned char>(60 + ((h >> 16) & 0x7F));
}

// ---- XML

// Returns an error text or empty when the tags balance. Comments, declarations, CDATA and self closing tags are skipped.
std::string xml_balance(const std::string& t) {
    std::vector<std::string> stack;
    size_t i = 0;
    while (i < t.size()) {
        if (t[i] != '<') {
            ++i;
            continue;
        }
        if (t.compare(i, 4, "<!--") == 0) {
            size_t e = t.find("-->", i + 4);
            if (e == std::string::npos) return "unterminated comment";
            i = e + 3;
            continue;
        }
        if (t.compare(i, 9, "<![CDATA[") == 0) {
            size_t e = t.find("]]>", i);
            if (e == std::string::npos) return "unterminated CDATA";
            i = e + 3;
            continue;
        }
        if (t.compare(i, 2, "<?") == 0 || t.compare(i, 2, "<!") == 0) {
            size_t e = t.find('>', i);
            if (e == std::string::npos) return "unterminated declaration";
            i = e + 1;
            continue;
        }
        size_t e = t.find('>', i);
        if (e == std::string::npos) return "a tag is never closed";
        std::string tag = t.substr(i + 1, e - i - 1);
        const bool closing = !tag.empty() && tag[0] == '/';
        const bool self = !tag.empty() && tag.back() == '/';
        std::string name = tag;
        if (closing) name = name.substr(1);
        const size_t sp = name.find_first_of(" \t\r\n/");
        if (sp != std::string::npos) name = name.substr(0, sp);
        if (closing) {
            if (stack.empty() || stack.back() != name) return "</" + name + "> does not match " + (stack.empty() ? "anything" : "<" + stack.back() + ">");
            stack.pop_back();
        } else if (!self) {
            stack.push_back(name);
        }
        i = e + 1;
    }
    return stack.empty() ? std::string() : "<" + stack.back() + "> is never closed";
}

std::vector<std::string> all_matches(const std::string& text, const std::regex& re) {
    std::vector<std::string> out;
    for (auto it = std::sregex_iterator(text.begin(), text.end(), re); it != std::sregex_iterator(); ++it) out.push_back((*it)[1]);
    return out;
}

std::vector<fs::path> files_with(const fs::path& dir, const std::string& ext) {
    std::vector<fs::path> out;
    std::error_code ec;
    if (!fs::exists(dir, ec)) return out;
    for (auto it = fs::recursive_directory_iterator(dir, ec); !ec && it != fs::recursive_directory_iterator(); it.increment(ec)) {
        if (it->is_regular_file(ec) && it->path().extension() == ext) out.push_back(it->path());
    }
    std::sort(out.begin(), out.end());
    return out;
}

// Scripts of a mod: .luau first, .lua still accepted.
std::vector<fs::path> lua_files(const fs::path& dir) {
    std::vector<fs::path> out = files_with(dir, ".luau");
    const std::vector<fs::path> more = files_with(dir, ".lua");
    out.insert(out.end(), more.begin(), more.end());
    return out;
}

// Keyed translation keys of one language folder.
std::map<std::string, std::string> keyed_of(const fs::path& lang_dir) {
    std::map<std::string, std::string> out;
    for (const fs::path& f : files_with(lang_dir / "Keyed", ".xml")) {
        const std::string text = read_all(f);
        std::regex re("<([A-Za-z_][A-Za-z0-9_.\\-]*)>([\\s\\S]*?)</\\1>");
        const size_t start = text.find("<LanguageData>");
        const std::string body = start == std::string::npos ? text : text.substr(start + 14);
        for (auto it = std::sregex_iterator(body.begin(), body.end(), re); it != std::sregex_iterator(); ++it) out[(*it)[1]] = (*it)[2];
    }
    return out;
}

// Keys a mod's Lua asks for with translate("Key"...).
std::set<std::string> translate_uses(const fs::path& mod_dir) {
    std::set<std::string> keys;
    std::regex re("translate\\(\\s*[\"']([^\"']+)[\"']");
    for (const fs::path& f : lua_files(mod_dir / "Lua")) {
        const std::string text = read_all(f);
        for (const std::string& k : all_matches(text, re)) keys.insert(k);
    }
    return keys;
}

std::string xml_esc(const std::string& s) {
    std::string out;
    for (char c : s) {
        if (c == '&') out += "&amp;";
        else if (c == '<') out += "&lt;";
        else if (c == '>') out += "&gt;";
        else out += c;
    }
    return out;
}

std::string csv_cell(const std::string& s) {
    std::string out = "\"";
    for (char c : s) {
        if (c == '"') out += "\"\"";
        else if (c == '\n') out += "\\n";
        else out += c;
    }
    return out + "\"";
}

std::vector<std::string> csv_split(const std::string& line) {
    std::vector<std::string> cells;
    std::string cur;
    bool q = false;
    for (size_t i = 0; i < line.size(); ++i) {
        char c = line[i];
        if (q) {
            if (c == '"' && i + 1 < line.size() && line[i + 1] == '"') { cur += '"'; ++i; }
            else if (c == '"') q = false;
            else cur += c;
        } else if (c == '"') q = true;
        else if (c == ',') { cells.push_back(cur); cur.clear(); }
        else if (c != '\r') cur += c;
    }
    cells.push_back(cur);
    return cells;
}

std::string unescape_nl(std::string s) {
    size_t p;
    while ((p = s.find("\\n")) != std::string::npos) s.replace(p, 2, "\n");
    return s;
}

bool semver_ok(const std::string& v) { return std::regex_match(v, std::regex("\\d+\\.\\d+\\.\\d+([-+][0-9A-Za-z.\\-]+)?")); }

}  // namespace

// ---------------------------------------------------------------- assets

int cmd_assets(const fs::path& mod_dir, const std::vector<std::string>& args) {
    ModMeta m;
    std::string err;
    load_meta(mod_dir, m, err);
    Report rep;
    const bool fix = has_flag(args, "--fix");
    const fs::path about = mod_dir / "About";
    fs::create_directories(about);
    unsigned char r = 80, g = 80, b = 120;
    colour_for(m.package_id.empty() ? mod_dir.filename().string() : m.package_id, r, g, b);

    const fs::path preview = about / "Preview.png";
    if (!fs::exists(preview)) {
        if (fix) {
            std::ofstream(preview, std::ios::binary) << make_png(640, 360, r, g, b);
            rep.warn("About/Preview.png was missing: wrote a 640x360 placeholder. Replace it with real art before publishing.");
        } else {
            rep.error("About/Preview.png is missing (the Workshop needs one). Run: rimkit mod assets --fix for a placeholder.");
        }
    } else {
        PngInfo p = png_info(preview);
        if (!p.valid) rep.error("About/Preview.png is not a PNG file.");
        else {
            if (p.w < 640 || p.h < 360) rep.warn("About/Preview.png is " + std::to_string(p.w) + "x" + std::to_string(p.h) + ". The Workshop shows it best at 640x360 or larger.");
            else rep.ok("Preview.png is " + std::to_string(p.w) + "x" + std::to_string(p.h));
            if (p.w * 9 != p.h * 16) rep.warn("Preview.png is not 16:9, the Workshop will crop it.");
            if (p.bytes > 1000000) rep.error("Preview.png is " + std::to_string(p.bytes / 1024) + " KB. Steam rejects preview images over 1 MB.");
        }
    }
    const fs::path icon = about / "ModIcon.png";
    if (!fs::exists(icon)) {
        if (fix) {
            std::ofstream(icon, std::ios::binary) << make_png(128, 128, r, g, b);
            rep.warn("About/ModIcon.png was missing: wrote a 128x128 placeholder.");
        } else {
            rep.warn("About/ModIcon.png is missing (optional, shown in the mod list). Run: rimkit mod assets --fix");
        }
    } else {
        PngInfo p = png_info(icon);
        if (!p.valid) rep.error("About/ModIcon.png is not a PNG file.");
        else if (p.w != p.h) rep.warn("ModIcon.png is not square (" + std::to_string(p.w) + "x" + std::to_string(p.h) + ").");
        else if (p.w < 64 || p.w > 512) rep.warn("ModIcon.png is " + std::to_string(p.w) + " px. Between 64 and 512 looks right in the mod list.");
        else rep.ok("ModIcon.png is " + std::to_string(p.w) + "x" + std::to_string(p.h));
    }
    std::cout << "assets: " << rep.errors << " error(s), " << rep.warnings << " warning(s)\n";
    return rep.errors ? 1 : 0;
}

// ---------------------------------------------------------------- content check

int content_check(const fs::path& mod_dir, bool quiet) {
    Report rep;
    rep.quiet = quiet;
    ModMeta m;
    std::string err;
    if (!load_meta(mod_dir, m, err)) {
        rep.error(err);
        return rep.errors;
    }
    if (m.name.empty() || m.package_id.empty()) rep.error("meta.lua needs name and package_id");
    if (!std::regex_match(m.package_id, std::regex("[A-Za-z0-9_]+(\\.[A-Za-z0-9_]+)+"))) rep.error("package_id " + m.package_id + " must look like author.modname");
    if (m.description.size() < 20) rep.warn("meta.description is very short");
    if (!m.mod_version.empty() && !semver_ok(m.mod_version)) rep.error("meta.mod_version " + m.mod_version + " is not a semantic version like 1.2.0");

    // Defs and patches: well formed, no duplicate def names, textures the mod itself ships.
    std::map<std::string, std::string> seen;
    std::regex def_re("<defName>\\s*([^<\\s]+)\\s*</defName>");
    std::regex tex_re("<texPath>\\s*([^<\\s]+)\\s*</texPath>");
    int def_files = 0;
    for (const char* sub : {"Defs", "Patches"}) {
        for (const fs::path& f : files_with(mod_dir / sub, ".xml")) {
            ++def_files;
            const std::string text = read_all(f);
            const std::string bad = xml_balance(text);
            const std::string rel = fs::relative(f, mod_dir).generic_string();
            if (!bad.empty()) {
                rep.error(rel + ": " + bad);
                continue;
            }
            if (std::string(sub) == "Defs") {
                for (const std::string& name : all_matches(text, def_re)) {
                    auto it = seen.find(name);
                    if (it != seen.end() && it->second != rel) rep.warn("defName " + name + " is defined in " + it->second + " and " + rel);
                    seen[name] = rel;
                }
                for (const std::string& tex : all_matches(text, tex_re)) {
                    const std::string first = tex.substr(0, tex.find('/'));
                    if (!fs::exists(mod_dir / "Textures" / first) && !fs::is_directory(mod_dir / "Textures" / first)) continue;  // vanilla path
                    bool found = false;
                    for (const char* ext : {".png", ".jpg", ".psd", ".tga"}) found = found || fs::exists(mod_dir / "Textures" / (tex + ext));
                    if (!found) {
                        // Graphic_Multi and Graphic_StackCount look for suffixed files too.
                        const fs::path dir = (mod_dir / "Textures" / tex).parent_path();
                        const std::string stem = fs::path(tex).filename().string();
                        std::error_code ec;
                        if (fs::exists(dir, ec)) for (auto& e : fs::directory_iterator(dir, ec)) found = found || e.path().filename().string().rfind(stem, 0) == 0;
                    }
                    if (!found) rep.error(fs::relative(f, mod_dir).generic_string() + ": texture " + tex + " is not in Textures/");
                }
            }
        }
    }
    if (def_files) rep.ok(std::to_string(def_files) + " Defs/Patches file(s) are well formed, " + std::to_string(seen.size()) + " defs");

    // Translation: every translate("Key") needs an English string, strings nobody uses are noise, other languages should keep up.
    const fs::path langs = mod_dir / "Languages";
    const auto english = keyed_of(langs / "English");
    const auto uses = translate_uses(mod_dir);
    for (const std::string& key : uses) {
        if (!english.count(key)) rep.error("translate(\"" + key + "\") has no English string in Languages/English/Keyed");
    }
    int unused = 0;
    for (const auto& kv : english) {
        if (kv.first == "RimLuaKitMarker" || kv.first.find("Marker") != std::string::npos) continue;
        if (!uses.count(kv.first)) ++unused;
    }
    if (unused) rep.warn(std::to_string(unused) + " English key(s) are not used by any translate(...) call in Lua (fine if Defs use them)");
    std::error_code ec;
    if (fs::exists(langs, ec)) {
        for (auto& e : fs::directory_iterator(langs, ec)) {
            if (!e.is_directory() || e.path().filename() == "English") continue;
            const auto other = keyed_of(e.path());
            int missing = 0;
            for (const auto& kv : english) if (!other.count(kv.first)) ++missing;
            if (missing) rep.warn(e.path().filename().string() + " is missing " + std::to_string(missing) + " key(s). rimkit mod i18n missing " + e.path().filename().string());
        }
    }

    // Capabilities: the permissions the Lua will need should be declared.
    std::string lua_all;
    for (const fs::path& f : lua_files(mod_dir / "Lua")) lua_all += read_all(f) + "\n";
    auto uses_api = [&](const char* needle) { return lua_all.find(needle) != std::string::npos; };
    auto declared = [&](const char* cap) { return std::find(m.capabilities.begin(), m.capabilities.end(), cap) != m.capabilities.end(); };
    if (m.caps_declared) {
        if ((uses_api("game.reflect") || uses_api("rim.reflect")) && !declared("reflect")) rep.error("the Lua uses game.reflect but meta.capabilities does not list \"reflect\"");
        if ((uses_api("game.hooks") || uses_api("game.tweaks") || uses_api("rim.hooks")) && !declared("hooks")) rep.error("the Lua uses hooks or tweaks but meta.capabilities does not list \"hooks\"");
        if ((uses_api("write_xml") || uses_api("patch.write")) && !declared("files")) rep.error("the Lua writes files but meta.capabilities does not list \"files\"");
    } else if (uses_api("game.reflect") || uses_api("game.hooks")) {
        rep.warn("meta.capabilities is not declared. Declare what the mod needs (reflect, hooks, files); mods without it get full access with a notice in the RimKit hub.");
    }

    // api_level 1 is opt-in strict mode: old names are removed, errors raise, capabilities are enforced.
    if (m.api_level < 0 || m.api_level > 1) rep.error("meta.api_level must be 0 or 1");
    if (m.api_level >= 1) {
        if (!m.caps_declared) rep.warn("meta.api_level = 1 enforces capabilities: with no meta.capabilities the mod gets none. Declare what it needs, or {} for nothing.");
        auto is_ident = [](char c) { return std::isalnum(static_cast<unsigned char>(c)) || c == '_'; };
        std::set<std::string> reported;
        for (const auto& a : rimlua::aliases::table().paths) {
            if (!a.deprecate || reported.count(a.old_path)) continue;
            size_t pos = 0;
            while ((pos = lua_all.find(a.old_path, pos)) != std::string::npos) {
                const size_t end = pos + a.old_path.size();
                const char before = pos == 0 ? '\0' : lua_all[pos - 1];
                const char after = end >= lua_all.size() ? '\0' : lua_all[end];
                if (!is_ident(after) && !is_ident(before) && before != '.' && before != ':' && before != '"' && before != '\'') {
                    rep.error("meta.api_level = 1 removes the old name " + a.old_path + ". Use " + a.new_path + " (rimkit migrate --write rewrites it).");
                    reported.insert(a.old_path);
                    break;
                }
                pos = end;
            }
        }
    }

    if (!quiet) std::cout << "content: " << rep.errors << " error(s), " << rep.warnings << " warning(s)\n";
    return rep.errors;
}

// ---------------------------------------------------------------- i18n

int cmd_i18n(const fs::path& mod_dir, const std::vector<std::string>& args) {
    if (args.empty()) {
        std::cerr << "Usage: rimkit mod i18n extract | missing <lang> | export <lang> [file.csv] | import <lang> <file.csv>\n";
        return 1;
    }
    const std::string sub = args[0];
    const fs::path langs = mod_dir / "Languages";
    const auto english = keyed_of(langs / "English");
    if (sub == "extract") {
        const auto uses = translate_uses(mod_dir);
        std::vector<std::string> add;
        for (const std::string& k : uses) if (!english.count(k)) add.push_back(k);
        if (add.empty()) {
            std::cout << "Every translate(...) key already has an English string.\n";
            return 0;
        }
        fs::create_directories(langs / "English" / "Keyed");
        const fs::path out = langs / "English" / "Keyed" / "Extracted.xml";
        std::ofstream f(out);
        f << "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<LanguageData>\n";
        for (const std::string& k : add) f << "  <" << k << ">TODO " << xml_esc(k) << "</" << k << ">\n";
        f << "</LanguageData>\n";
        std::cout << "Wrote " << add.size() << " key(s) to " << out.string() << ". Replace the TODO texts.\n";
        return 0;
    }
    if (args.size() < 2) {
        std::cerr << "Name a language folder, for example German.\n";
        return 1;
    }
    const std::string lang = args[1];
    const auto other = keyed_of(langs / lang);
    if (sub == "missing") {
        int n = 0;
        for (const auto& kv : english) {
            if (kv.first.find("Marker") != std::string::npos) continue;
            if (!other.count(kv.first)) {
                std::cout << kv.first << "\n";
                ++n;
            }
        }
        std::cout << n << " key(s) missing in " << lang << "\n";
        return n ? 1 : 0;
    }
    if (sub == "export") {
        const fs::path out = args.size() >= 3 ? fs::path(args[2]) : mod_dir / (lang + ".csv");
        std::ofstream f(out);
        f << "key,english," << lang << "\n";
        for (const auto& kv : english) {
            if (kv.first.find("Marker") != std::string::npos) continue;
            auto it = other.find(kv.first);
            f << csv_cell(kv.first) << "," << csv_cell(kv.second) << "," << csv_cell(it == other.end() ? "" : it->second) << "\n";
        }
        std::cout << "Wrote " << out.string() << " (" << english.size() << " rows). Fill the " << lang << " column and run: rimkit mod i18n import " << lang << " " << out.filename().string() << "\n";
        return 0;
    }
    if (sub == "import") {
        if (args.size() < 3 || !fs::exists(args[2])) {
            std::cerr << "Give the csv file to import.\n";
            return 1;
        }
        std::ifstream in(args[2]);
        std::string line;
        std::getline(in, line);
        std::vector<std::pair<std::string, std::string>> rows;
        while (std::getline(in, line)) {
            auto c = csv_split(line);
            if (c.size() >= 3 && !c[0].empty() && !c[2].empty()) rows.emplace_back(c[0], unescape_nl(c[2]));
        }
        fs::create_directories(langs / lang / "Keyed");
        std::ofstream f(langs / lang / "Keyed" / "Imported.xml");
        f << "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<LanguageData>\n";
        int n = 0;
        for (auto& r : rows) {
            f << "  <" << r.first << ">" << xml_esc(r.second) << "</" << r.first << ">\n";
            ++n;
        }
        f << "</LanguageData>\n";
        std::cout << "Imported " << n << " string(s) into Languages/" << lang << "/Keyed/Imported.xml\n";
        return 0;
    }
    std::cerr << "Unknown i18n command " << sub << "\n";
    return 1;
}

// ---------------------------------------------------------------- release check

int cmd_release_check(const fs::path& mod_dir) {
    Report rep;
    ModMeta m;
    std::string err;
    std::cout << "release check for " << mod_dir.filename().string() << "\n";
    if (!load_meta(mod_dir, m, err)) {
        rep.error(err);
        return 1;
    }
    // Metadata
    if (m.mod_version.empty()) rep.error("meta.mod_version is not set (use a semantic version like 1.0.0)");
    else if (!semver_ok(m.mod_version)) rep.error("meta.mod_version " + m.mod_version + " is not a semantic version");
    else rep.ok("mod version " + m.mod_version);
    if (m.description.size() < 60 && !fs::exists(mod_dir / "Workshop.md")) rep.warn("the description is short. Fill Workshop.md: players decide on it.");
    if (m.game_versions.empty()) rep.error("no supported game version");
    // Changelog
    fs::path changelog = mod_dir / "CHANGELOG.md";
    if (!fs::exists(changelog)) rep.error("CHANGELOG.md is missing");
    else if (!m.mod_version.empty() && read_all(changelog).find(m.mod_version) == std::string::npos) rep.error("CHANGELOG.md has no entry for " + m.mod_version);
    else rep.ok("changelog has an entry for " + m.mod_version);
    // Legal and attribution
    bool license = false;
    for (const char* n : {"LICENSE", "LICENSE.md", "LICENSE.txt"}) license = license || fs::exists(mod_dir / n);
    if (!license) rep.error("no LICENSE file. Say how others may use, change and share the mod.");
    else rep.ok("licence file present");
    const bool has_assets = fs::exists(mod_dir / "Textures") || fs::exists(mod_dir / "Sounds");
    if (has_assets && !fs::exists(mod_dir / "CREDITS.md")) rep.error("the mod ships Textures or Sounds but has no CREDITS.md naming who made them and under which licence");
    std::string text = m.description;
    if (fs::exists(mod_dir / "Workshop.md")) text += read_all(mod_dir / "Workshop.md");
    std::string lower = text;
    std::transform(lower.begin(), lower.end(), lower.begin(), [](unsigned char c) { return static_cast<char>(std::tolower(c)); });
    if (lower.find("official") != std::string::npos || lower.find("endorsed by ludeon") != std::string::npos) rep.warn("the description says \"official\". Ludeon does not endorse mods.");
    if (lower.find("rimworld") == std::string::npos) rep.warn("the description never says what game this is for");
    // Safety
    if (!m.caps_declared) rep.warn("meta.capabilities is not declared. Declaring it shows players what the mod may do.");
    // Assets
    const std::vector<std::string> none;
    if (cmd_assets(mod_dir, none) != 0) rep.error("preview or icon problems, see above");
    // Content
    if (content_check(mod_dir, false) != 0) rep.error("content check failed, see above");
    // Tests
    if (!fs::exists(mod_dir / "Tests") && !fs::exists(mod_dir / "tests")) rep.warn("no Tests folder. A few game.test cases catch most regressions: rimkit mod test");
    std::cout << "Legal reminder: do not ship game files, the RimWorld logo or other people's art without their permission, and follow Ludeon's modding rules (rimworldgame.com/eula).\n";
    std::cout << "release check: " << rep.errors << " failed, " << rep.warnings << " warning(s)\n";
    return rep.errors ? 1 : 0;
}

// ---------------------------------------------------------------- conform

namespace {

// Every .lua and .luau file under a folder, skipping Tests/Game when asked.
std::vector<fs::path> lua_files(const fs::path& dir, bool skip_game_tests) {
    std::vector<fs::path> out;
    std::error_code ec;
    if (!fs::is_directory(dir, ec)) return out;
    for (auto it = fs::recursive_directory_iterator(dir, ec); !ec && it != fs::recursive_directory_iterator(); it.increment(ec)) {
        if (!it->is_regular_file(ec)) continue;
        const fs::path ext = it->path().extension();
        if (ext != ".lua" && ext != ".luau") continue;
        const std::string generic = it->path().generic_string();
        if (skip_game_tests && (generic.find("/Tests/Game/") != std::string::npos || generic.find("/tests/game/") != std::string::npos)) continue;
        out.push_back(it->path());
    }
    return out;
}

}  // namespace

int cmd_conform(const fs::path& mod_dir, const std::vector<std::string>& args) {
    Report rep;
    ModMeta m;
    std::string err;
    int wanted = 1;
    std::string game_report;
    for (size_t i = 0; i + 1 < args.size(); ++i) {
        if (args[i] == "--level") wanted = std::atoi(args[i + 1].c_str());
        if (args[i] == "--report") game_report = args[i + 1];
    }
    std::cout << "RimKit Standard check for " << mod_dir.filename().string() << "\n";
    if (!load_meta(mod_dir, m, err)) {
        std::cout << "  [fail] " << err << "\n";
        return 1;
    }

    // Level 1: declared. The mod says what it is and what it may do, and ships what players need.
    std::cout << "level 1, declared\n";
    Report l1;
    if (m.api_level < 1) l1.error("meta.api_level = 1 is not set. Strict mode refuses removed names and unchecked results.");
    else l1.ok("api_level " + std::to_string(m.api_level));
    if (!m.caps_declared) l1.error("meta.capabilities is not declared. Write the list, an empty one is fine.");
    else l1.ok("capabilities declared");
    if (m.mod_version.empty() || !semver_ok(m.mod_version)) l1.error("meta.mod_version is missing or not a semantic version");
    else l1.ok("mod version " + m.mod_version);
    const fs::path changelog = mod_dir / "CHANGELOG.md";
    if (!fs::exists(changelog) || (!m.mod_version.empty() && read_all(changelog).find(m.mod_version) == std::string::npos)) l1.error("CHANGELOG.md has no entry for " + m.mod_version);
    else l1.ok("changelog entry");
    bool license = false;
    for (const char* n : {"LICENSE", "LICENSE.md", "LICENSE.txt"}) license = license || fs::exists(mod_dir / n);
    if (!license) l1.error("no LICENSE file");
    else l1.ok("licence file");
    if (content_check(mod_dir, true) != 0) l1.error("rimkit mod check reports errors");
    else l1.ok("content check");
    int level = l1.errors == 0 ? 1 : 0;

    // Level 2: tested. Logic runs against the mock host and survives a hot reload.
    std::cout << "level 2, tested\n";
    Report l2;
    const fs::path tests = fs::exists(mod_dir / "Tests") ? mod_dir / "Tests" : mod_dir / "tests";
    const std::vector<fs::path> test_files = lua_files(tests, true);
    bool reload_test = false;
    for (const fs::path& f : test_files) {
        const std::string text = read_all(f);
        if (text.find("reload(") != std::string::npos) reload_test = true;
    }
    if (test_files.empty()) l2.error("no tests in Tests/. Run rimkit mod gen-tests for a start.");
    else l2.ok(std::to_string(test_files.size()) + " test file(s)");
    if (!test_files.empty() && !reload_test) l2.error("no test calls reload(). A test must show the mod survives a hot reload: rimkit mod gen-tests writes one.");
    else if (reload_test) l2.ok("a test covers hot reload");
    int legacy = 0;
    for (const fs::path& f : lua_files(mod_dir / "Lua", false)) {
        std::istringstream lines(read_all(f));
        std::string line;
        while (std::getline(lines, line)) {
            const size_t first = line.find_first_not_of(" \t");
            if (first == std::string::npos || line.compare(first, 2, "--") == 0) continue;
            if (std::regex_search(line, std::regex("(^|[^A-Za-z0-9_.])rim\\.(pawn|map|thing|faction|hooks|events|on_load|on_tick|log|message)\\b"))) ++legacy;
        }
    }
    if (legacy > 0) l2.error(std::to_string(legacy) + " use(s) of the pre-1.0 rim.* names in Lua/. Run rimkit migrate --write.");
    else l2.ok("no pre-1.0 names");
    if (l2.errors == 0 && level == 1) {
        std::cout << "  running the tests\n";
        if (cmd_test(mod_dir, {}) != 0) l2.error("the tests do not pass");
        else l2.ok("tests pass");
    }
    if (l2.errors == 0 && level == 1) level = 2;

    // Level 3: verified in the game. Scripts in Tests/Game ran in a real game and the report says nothing failed.
    std::cout << "level 3, verified in the game\n";
    Report l3;
    const std::vector<fs::path> game_files = [&]() {
        std::vector<fs::path> files;
        for (const fs::path& f : lua_files(tests / "Game", false)) files.push_back(f);
        return files;
    }();
    bool has_suite = false;
    for (const fs::path& f : game_files) has_suite = has_suite || read_all(f).find("itest.suite") != std::string::npos;
    if (!has_suite) l3.error("no game.itest.suite in Tests/Game");
    else l3.ok("in-game tests exist");
    if (game_report.empty()) {
        l3.error("pass --report <file> with the report of rimkit mod test --in-game");
    } else {
        rimlua::json::Value root;
        if (!rimlua::json::parse(read_all(game_report), root) || !root.is_object()) l3.error(game_report + " is not a test report");
        else if (root.get_int("fail", 1) != 0 || root.get_int("pass", 0) == 0) l3.error("the in-game report has failures or no passing tests");
        else l3.ok("in-game report: " + std::to_string(root.get_int("pass")) + " passed");
    }
    if (l3.errors == 0 && level == 2) level = 3;

    static const char* const kNames[] = {"not conforming", "Declared", "Tested", "Verified in the game"};
    std::cout << "RimKit Standard level: " << level << " (" << kNames[level] << ")\n";
    if (level > 0) std::cout << "For your Workshop.md: Built to the RimKit Standard, level " << level << " (" << kNames[level] << ").\n";
    return level >= wanted ? 0 : 1;
}

// ---------------------------------------------------------------- scaffold

void scaffold_extras(const fs::path& out, const std::string& name, const std::string& package_id) {
    auto write_if_missing = [&](const fs::path& p, const std::string& text) {
        if (fs::exists(p)) return;
        fs::create_directories(p.parent_path());
        std::ofstream(p) << text;
    };
    std::time_t t = std::time(nullptr);
    std::tm tm{};
#ifdef _WIN32
    localtime_s(&tm, &t);
#else
    localtime_r(&t, &tm);
#endif
    const int year = 1900 + tm.tm_year;
    write_if_missing(out / "LICENSE", "MIT License\n\nCopyright (c) " + std::to_string(year) + " the authors of " + name +
                                          "\n\nPermission is hereby granted, free of charge, to any person obtaining a copy of this software and associated\n"
                                          "documentation files (the \"Software\"), to deal in the Software without restriction, including without limitation the\n"
                                          "rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit\n"
                                          "persons to whom the Software is furnished to do so, subject to the following conditions:\n\n"
                                          "The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.\n\n"
                                          "THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED.\n");
    write_if_missing(out / "CREDITS.md", "# Credits\n\nList everyone whose work is in this mod, with the licence it was shared under.\n\n- Code: the authors of " + name +
                                             "\n- Art and sounds: none yet\n- Built with RimKit and Harmony\n");
    write_if_missing(out / "CHANGELOG.md", "# Changelog\n\n## 0.1.0\n\n- First version.\n");
    write_if_missing(out / "Workshop.md", "[h1]" + name + "[/h1]\n\nOne sentence that says what the mod does for the player.\n\n[h2]Features[/h2]\n[list]\n[*] First feature\n[*] Second feature\n[/list]\n\n"
                                          "[h2]Requirements[/h2]\n[list]\n[*] Harmony\n[*] RimKit\n[/list]\n\n[h2]Compatibility[/h2]\nSafe to add to a running save. Removing it later leaves no errors.\n\n"
                                          "[h2]Credits[/h2]\nSee CREDITS.md. Source and issues: your repository link.\n");
    write_if_missing(out / "Tests" / "main_test.luau",
                     "-- Run with: rimkit mod test\n"
                     "-- Tests run against a mock host, so no game is needed. Mock what the game would answer, call your code, check the result.\n"
                     "local t = game.test\n\n"
                     "t.describe(\"" + name + "\", function()\n"
                     "  t.it(\"says it loaded\", function()\n"
                     "    t.start()                                  -- runs the mod's on_load handlers\n"
                     "    t.expect(t.logged(\"loaded\")).to_be(true)   -- what the mod logged or showed\n"
                     "  end)\n"
                     "end)\n");
    (void)package_id;
}

}  // namespace rkcli
