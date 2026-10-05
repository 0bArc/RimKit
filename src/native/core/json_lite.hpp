#pragma once

// Small strict JSON reader and writer used by the hook and event channels.
// It replaces ad hoc substring scanning, so strings that contain braces or quotes are safe.

#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <string>
#include <utility>
#include <vector>

namespace rimlua {
namespace json {

struct Value {
    enum class Type { Null, Bool, Int, Float, String, Array, Object };

    Type type = Type::Null;
    bool b = false;
    long long i = 0;
    double d = 0.0;
    std::string s;
    std::vector<Value> items;         // Array items, or Object values.
    std::vector<std::string> keys;    // Object keys, parallel to items.

    bool is_null() const { return type == Type::Null; }
    bool is_object() const { return type == Type::Object; }
    bool is_array() const { return type == Type::Array; }
    bool is_string() const { return type == Type::String; }
    bool is_number() const { return type == Type::Int || type == Type::Float; }
    double as_double() const { return type == Type::Int ? static_cast<double>(i) : d; }

    const Value* find(const std::string& key) const {
        if (type != Type::Object) {
            return nullptr;
        }
        for (size_t n = 0; n < keys.size(); ++n) {
            if (keys[n] == key) {
                return &items[n];
            }
        }
        return nullptr;
    }

    std::string get_string(const std::string& key, const std::string& fallback = std::string()) const {
        const Value* v = find(key);
        return (v && v->type == Type::String) ? v->s : fallback;
    }

    long long get_int(const std::string& key, long long fallback = 0) const {
        const Value* v = find(key);
        if (!v) {
            return fallback;
        }
        if (v->type == Type::Int) {
            return v->i;
        }
        if (v->type == Type::Float) {
            return static_cast<long long>(v->d);
        }
        return fallback;
    }

    bool get_bool(const std::string& key, bool fallback = false) const {
        const Value* v = find(key);
        return (v && v->type == Type::Bool) ? v->b : fallback;
    }
};

namespace detail {

constexpr int kMaxDepth = 64;

inline void skip_ws(const std::string& t, size_t& p) {
    while (p < t.size() && (t[p] == ' ' || t[p] == '\t' || t[p] == '\n' || t[p] == '\r')) {
        ++p;
    }
}

inline void append_utf8(std::string& out, unsigned cp) {
    if (cp < 0x80) {
        out += static_cast<char>(cp);
    } else if (cp < 0x800) {
        out += static_cast<char>(0xC0 | (cp >> 6));
        out += static_cast<char>(0x80 | (cp & 0x3F));
    } else if (cp < 0x10000) {
        out += static_cast<char>(0xE0 | (cp >> 12));
        out += static_cast<char>(0x80 | ((cp >> 6) & 0x3F));
        out += static_cast<char>(0x80 | (cp & 0x3F));
    } else {
        out += static_cast<char>(0xF0 | (cp >> 18));
        out += static_cast<char>(0x80 | ((cp >> 12) & 0x3F));
        out += static_cast<char>(0x80 | ((cp >> 6) & 0x3F));
        out += static_cast<char>(0x80 | (cp & 0x3F));
    }
}

inline bool read_hex4(const std::string& t, size_t& p, unsigned& out) {
    if (p + 4 > t.size()) {
        return false;
    }
    unsigned v = 0;
    for (int n = 0; n < 4; ++n) {
        char c = t[p + n];
        v <<= 4;
        if (c >= '0' && c <= '9') {
            v |= static_cast<unsigned>(c - '0');
        } else if (c >= 'a' && c <= 'f') {
            v |= static_cast<unsigned>(c - 'a' + 10);
        } else if (c >= 'A' && c <= 'F') {
            v |= static_cast<unsigned>(c - 'A' + 10);
        } else {
            return false;
        }
    }
    p += 4;
    out = v;
    return true;
}

inline bool parse_string(const std::string& t, size_t& p, std::string& out) {
    if (p >= t.size() || t[p] != '"') {
        return false;
    }
    ++p;
    out.clear();
    while (p < t.size()) {
        char c = t[p++];
        if (c == '"') {
            return true;
        }
        if (c != '\\') {
            out += c;
            continue;
        }
        if (p >= t.size()) {
            return false;
        }
        char e = t[p++];
        switch (e) {
            case '"': out += '"'; break;
            case '\\': out += '\\'; break;
            case '/': out += '/'; break;
            case 'b': out += '\b'; break;
            case 'f': out += '\f'; break;
            case 'n': out += '\n'; break;
            case 'r': out += '\r'; break;
            case 't': out += '\t'; break;
            case 'u': {
                unsigned cp = 0;
                if (!read_hex4(t, p, cp)) {
                    return false;
                }
                if (cp >= 0xD800 && cp <= 0xDBFF && p + 1 < t.size() && t[p] == '\\' && t[p + 1] == 'u') {
                    size_t q = p + 2;
                    unsigned lo = 0;
                    if (read_hex4(t, q, lo) && lo >= 0xDC00 && lo <= 0xDFFF) {
                        cp = 0x10000 + ((cp - 0xD800) << 10) + (lo - 0xDC00);
                        p = q;
                    }
                }
                append_utf8(out, cp);
                break;
            }
            default:
                return false;
        }
    }
    return false;
}

inline bool parse_value(const std::string& t, size_t& p, Value& out, int depth) {
    if (depth > kMaxDepth) {
        return false;
    }
    skip_ws(t, p);
    if (p >= t.size()) {
        return false;
    }
    char c = t[p];
    if (c == '{') {
        ++p;
        out.type = Value::Type::Object;
        skip_ws(t, p);
        if (p < t.size() && t[p] == '}') {
            ++p;
            return true;
        }
        while (true) {
            skip_ws(t, p);
            std::string key;
            if (!parse_string(t, p, key)) {
                return false;
            }
            skip_ws(t, p);
            if (p >= t.size() || t[p] != ':') {
                return false;
            }
            ++p;
            Value v;
            if (!parse_value(t, p, v, depth + 1)) {
                return false;
            }
            out.keys.push_back(std::move(key));
            out.items.push_back(std::move(v));
            skip_ws(t, p);
            if (p < t.size() && t[p] == ',') {
                ++p;
                continue;
            }
            if (p < t.size() && t[p] == '}') {
                ++p;
                return true;
            }
            return false;
        }
    }
    if (c == '[') {
        ++p;
        out.type = Value::Type::Array;
        skip_ws(t, p);
        if (p < t.size() && t[p] == ']') {
            ++p;
            return true;
        }
        while (true) {
            Value v;
            if (!parse_value(t, p, v, depth + 1)) {
                return false;
            }
            out.items.push_back(std::move(v));
            skip_ws(t, p);
            if (p < t.size() && t[p] == ',') {
                ++p;
                continue;
            }
            if (p < t.size() && t[p] == ']') {
                ++p;
                return true;
            }
            return false;
        }
    }
    if (c == '"') {
        out.type = Value::Type::String;
        return parse_string(t, p, out.s);
    }
    if (t.compare(p, 4, "true") == 0) {
        p += 4;
        out.type = Value::Type::Bool;
        out.b = true;
        return true;
    }
    if (t.compare(p, 5, "false") == 0) {
        p += 5;
        out.type = Value::Type::Bool;
        out.b = false;
        return true;
    }
    if (t.compare(p, 4, "null") == 0) {
        p += 4;
        out.type = Value::Type::Null;
        return true;
    }
    if (c == '-' || (c >= '0' && c <= '9')) {
        size_t start = p;
        bool is_float = false;
        if (t[p] == '-') {
            ++p;
        }
        while (p < t.size() && ((t[p] >= '0' && t[p] <= '9') || t[p] == '.' || t[p] == 'e' || t[p] == 'E' ||
                                t[p] == '+' || t[p] == '-')) {
            if (t[p] == '.' || t[p] == 'e' || t[p] == 'E') {
                is_float = true;
            }
            ++p;
        }
        std::string num = t.substr(start, p - start);
        if (num.empty() || num == "-") {
            return false;
        }
        if (is_float) {
            out.type = Value::Type::Float;
            out.d = std::strtod(num.c_str(), nullptr);
        } else {
            out.type = Value::Type::Int;
            out.i = std::strtoll(num.c_str(), nullptr, 10);
        }
        return true;
    }
    return false;
}

}  // namespace detail

// Parses a complete JSON document. Returns false on any syntax error or trailing data.
inline bool parse(const std::string& text, Value& out) {
    out = Value();
    size_t p = 0;
    if (!detail::parse_value(text, p, out, 0)) {
        out = Value();
        return false;
    }
    detail::skip_ws(text, p);
    if (p != text.size()) {
        out = Value();
        return false;
    }
    return true;
}

inline void write_string(std::string& out, const std::string& s) {
    out += '"';
    for (unsigned char c : s) {
        switch (c) {
            case '"': out += "\\\""; break;
            case '\\': out += "\\\\"; break;
            case '\n': out += "\\n"; break;
            case '\r': out += "\\r"; break;
            case '\t': out += "\\t"; break;
            case '\b': out += "\\b"; break;
            case '\f': out += "\\f"; break;
            default:
                if (c < 0x20) {
                    char buf[8];
                    std::snprintf(buf, sizeof(buf), "\\u%04x", static_cast<unsigned>(c));
                    out += buf;
                } else {
                    out += static_cast<char>(c);
                }
                break;
        }
    }
    out += '"';
}

inline void write_double(std::string& out, double d) {
    if (d != d || d > 1.7e308 || d < -1.7e308) {
        out += "null";
        return;
    }
    char buf[40];
    std::snprintf(buf, sizeof(buf), "%.9g", d);
    out += buf;
}

}  // namespace json
}  // namespace rimlua
