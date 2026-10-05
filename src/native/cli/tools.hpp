#pragma once

// Ecosystem commands of the rimkit CLI: tests, diagnostics, publishing, assets, content checks, translation and release checks.
#include <filesystem>
#include <functional>
#include <map>
#include <string>
#include <vector>

namespace rkcli {

namespace fs = std::filesystem;

struct ModMeta {
    std::string name, author, package_id, description, mod_version, url, license, changelog, workshop_id;
    std::vector<std::string> game_versions, capabilities, tags, depends, load_after, load_before, incompatible;
    std::map<std::string, std::vector<std::string>> version_folders;
    bool caps_declared = false;
    int perf_budget_us = 300;
    int api_level = 0;
};

// Set by main.cpp: copies a mod's shippable folders.
extern std::function<void(const fs::path&, const fs::path&)> copy_payload;

bool load_meta(const fs::path& mod_dir, ModMeta& meta, std::string& err);
// About/RimKit.json (version, capabilities, budget) and LoadFolders.xml, written next to About.xml by rimkit mod sync.
int write_extras(const fs::path& mod_dir);
// LICENSE, CREDITS, CHANGELOG, Workshop.md and a first test for a new mod.
void scaffold_extras(const fs::path& out, const std::string& name, const std::string& package_id);

int cmd_test(const fs::path& mod_dir, const std::vector<std::string>& args);
int cmd_diag(const std::vector<std::string>& args);
int cmd_publish(const fs::path& mod_dir, const std::vector<std::string>& args);
int cmd_assets(const fs::path& mod_dir, const std::vector<std::string>& args);
int cmd_i18n(const fs::path& mod_dir, const std::vector<std::string>& args);
// Defs, patches, textures and translation keys. Returns the number of errors. Warnings print but do not count.
int content_check(const fs::path& mod_dir, bool quiet);
int cmd_release_check(const fs::path& mod_dir);

}  // namespace rkcli
