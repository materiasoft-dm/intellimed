"""Generate src/IntelliMed.Client/src/api/types.gen.ts from the C# contract in IntelliMed.Core.

The React client talks to the API in JSON, so its types have to match the DTOs exactly. Rather than
keep two copies in step by hand, this reads the C# source and writes the TypeScript:

  * every public class in Core/DTOs        -> an interface (camelCase properties, as the API emits them)
  * every enum in Core/DTOs and Core/Entities -> a numeric TS enum (the API has no string enum converter)
  * static classes of string constants      -> a frozen const object (e.g. EmailEventKeys)

Run it after changing a DTO or enum:  python scripts/generate-client-types.py
The DTOs are plain property bags, so a careful regex reader is enough; anything it cannot map becomes
`unknown` and is listed on stdout so it gets looked at instead of silently mistyped.
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CORE = os.path.join(ROOT, "src", "IntelliMed.Core")
OUT = os.path.join(ROOT, "src", "IntelliMed.Client", "src", "api", "types.gen.ts")

NUMBER = {"int", "long", "short", "byte", "sbyte", "uint", "ulong", "ushort", "decimal", "double", "float"}
STRING = {"string", "char", "Guid", "DateTime", "DateTimeOffset", "DateOnly", "TimeSpan", "TimeOnly"}
LISTS = {"List", "IList", "IEnumerable", "ICollection", "IReadOnlyList", "IReadOnlyCollection", "HashSet",
         "ISet", "IReadOnlySet"}
DICTS = {"Dictionary", "IDictionary", "IReadOnlyDictionary"}

unmapped = []


def strip_comments(src: str) -> str:
    out, i, n = [], 0, len(src)
    while i < n:
        if src.startswith("//", i):
            j = src.find("\n", i)
            i = n if j < 0 else j
        elif src.startswith("/*", i):
            j = src.find("*/", i + 2)
            i = n if j < 0 else j + 2
        elif src[i] == '"' or src.startswith('@"', i):
            # copy string literals verbatim so a // inside one is not taken for a comment
            verbatim = src[i] == "@"
            j = i + (2 if verbatim else 1)
            while j < n:
                if verbatim and src.startswith('""', j):
                    j += 2
                    continue
                if not verbatim and src[j] == "\\":
                    j += 2
                    continue
                if src[j] == '"':
                    break
                j += 1
            out.append(src[i:j + 1])
            i = j + 1
        else:
            out.append(src[i])
            i += 1
    return "".join(out)


def strip_attributes(src: str) -> str:
    # [Required], [MaxLength(20)], [Display(Name = "x")] ... on their own or before a member
    return re.sub(r"\[(?:[A-Za-z_][\w.]*)(?:\([^\]]*\))?(?:\s*,\s*[A-Za-z_][\w.]*(?:\([^\]]*\))?)*\]", " ", src)


def camel(name: str) -> str:
    """System.Text.Json's JsonNamingPolicy.CamelCase, character for character."""
    if not name or not name[0].isupper():
        return name
    chars = list(name)
    for i in range(len(chars)):
        if i == 1 and not chars[i].isupper():
            break
        has_next = i + 1 < len(chars)
        if i > 0 and has_next and not chars[i + 1].isupper():
            if chars[i + 1] == " ":
                chars[i] = chars[i].lower()
            break
        chars[i] = chars[i].lower()
    return "".join(chars)


def block_after(src: str, start: int) -> tuple[str, int]:
    """The text between the first '{' at/after start and its matching '}'."""
    open_ = src.index("{", start)
    depth, i = 0, open_
    while i < len(src):
        c = src[i]
        if c == "{":
            depth += 1
        elif c == "}":
            depth -= 1
            if depth == 0:
                return src[open_ + 1:i], i + 1
        elif c == '"':
            j = i + 1
            while j < len(src) and src[j] != '"':
                j += 2 if src[j] == "\\" else 1
            i = j
        i += 1
    raise ValueError("unbalanced braces")


def top_level(body: str) -> str:
    """Body text with nested { ... } blocks blanked out, so only depth-1 members remain visible."""
    out, depth = [], 0
    for c in body:
        if c == "{":
            depth += 1
            out.append("{" if depth == 1 else " ")
        elif c == "}":
            out.append("}" if depth == 1 else " ")
            depth -= 1
        else:
            out.append(c if depth == 0 else " ")
    return "".join(out)


def split_generic_args(s: str) -> list[str]:
    args, depth, cur = [], 0, []
    for c in s:
        if c == "<":
            depth += 1
        elif c == ">":
            depth -= 1
        if c == "," and depth == 0:
            args.append("".join(cur).strip())
            cur = []
        else:
            cur.append(c)
    if cur:
        args.append("".join(cur).strip())
    return args


def ts_type(cs: str, known: set[str], generic_params: set[str], where: str) -> str:
    cs = cs.strip()
    nullable = cs.endswith("?")
    if nullable:
        cs = cs[:-1].strip()
    t = ts_type_inner(cs, known, generic_params, where)
    return f"{t} | null" if nullable else t


def ts_type_inner(cs: str, known: set[str], generic_params: set[str], where: str) -> str:
    cs = cs.replace("System.", "")
    if cs.endswith("[]"):
        if cs == "byte[]":
            return "string"  # System.Text.Json writes byte[] as base64
        return f"{wrap(ts_type(cs[:-2], known, generic_params, where))}[]"
    m = re.fullmatch(r"([\w.]+)\s*<(.+)>", cs)
    if m:
        name, args = m.group(1).split(".")[-1], split_generic_args(m.group(2))
        if name in LISTS and len(args) == 1:
            return f"{wrap(ts_type(args[0], known, generic_params, where))}[]"
        if name in DICTS and len(args) == 2:
            return f"Record<string, {ts_type(args[1], known, generic_params, where)}>"
        if name == "Nullable" and len(args) == 1:
            return f"{ts_type(args[0], known, generic_params, where)} | null"
        if name in known:
            return f"{name}<{', '.join(ts_type(a, known, generic_params, where) for a in args)}>"
        unmapped.append(f"{where}: {cs}")
        return "unknown"
    name = cs.split(".")[-1]
    if name in NUMBER:
        return "number"
    if name in STRING:
        return "string"
    if name == "bool":
        return "boolean"
    if name in ("object", "dynamic", "JsonElement"):
        return "unknown"
    if name in known or name in generic_params:
        return name
    unmapped.append(f"{where}: {cs}")
    return "unknown"


def wrap(t: str) -> str:
    return f"({t})" if "|" in t else t


def default_value(cs_type: str, init: str | None, enums: dict, classes: dict, where: str) -> str:
    """The TS literal for a property's C# default (its initializer, else default(T))."""
    t = cs_type.strip().replace("System.", "")
    nullable = t.endswith("?")
    base = t[:-1].strip() if nullable else t
    bname = re.sub(r"<.*>", "", base).split(".")[-1]
    is_list = base.endswith("[]") and base != "byte[]" or bname in LISTS
    is_dict = bname in DICTS

    if init is not None:
        v = init.strip()
        if v in ("string.Empty", "String.Empty", '""'):
            return "''"
        if v == "null" or v == "default":
            return "null" if nullable or v == "null" else default_value(cs_type, None, enums, classes, where)
        if re.fullmatch(r'@?"(?:[^"\\]|\\.)*"', v):
            return v.lstrip("@")
        if v in ("true", "false"):
            return v
        if re.fullmatch(r"-?\d+(\.\d+)?[mMdDfFlL]?", v):
            return re.sub(r"[mMdDfFlL]$", "", v)
        if re.fullmatch(r"new\s*(\(\s*\))?|new\s+[\w.<>, ]+\(\s*\)|\[\s*\]|(Array|Enumerable)\.Empty<.*>\(\)|new\s+[\w.<>, ]+\[\s*0\s*\]", v):
            if is_list:
                return "[]"
            if is_dict:
                return "{}"
            if bname in classes and not classes[bname][1]:
                return f"default{bname}()"
            return "{}"
        em = re.fullmatch(r"(\w+)\.(\w+)", v)
        if em and em.group(1) in enums:
            return f"{em.group(1)}.{em.group(2)}"
        if v == "DateTime.Today":
            return "today()"
        if v in ("DateTime.Now", "DateTime.UtcNow", "DateTimeOffset.Now", "DateTimeOffset.UtcNow"):
            return "now()"
        tm = re.fullmatch(r"TimeSpan\.From(Hours|Minutes|Seconds)\((\d+)\)", v)
        if tm:
            secs = int(tm.group(2)) * {"Hours": 3600, "Minutes": 60, "Seconds": 1}[tm.group(1)]
            return f"'{secs // 3600:02d}:{secs % 3600 // 60:02d}:{secs % 60:02d}'"
        tm = re.fullmatch(r"new TimeSpan\((\d+),\s*(\d+),\s*(\d+)\)", v)
        if tm:
            return f"'{int(tm.group(1)):02d}:{int(tm.group(2)):02d}:{int(tm.group(3)):02d}'"
        unmapped_defaults.append(f"{where} = {v}")

    if nullable:
        return "null"
    if is_list:
        return "[]"
    if is_dict:
        return "{}"
    if bname in NUMBER:
        return "0"
    if bname == "bool":
        return "false"
    if bname in ("string", "char"):
        return "''"
    if bname in ("DateTime", "DateTimeOffset"):
        return "'0001-01-01T00:00:00'"
    if bname == "DateOnly":
        return "'0001-01-01'"
    if bname in ("TimeSpan", "TimeOnly"):
        return "'00:00:00'"
    if bname == "Guid":
        return "'00000000-0000-0000-0000-000000000000'"
    if bname in enums:
        members = enums[bname][1]
        zero = next((m for m, v in members if v == 0), None)
        return f"{bname}.{zero}" if zero else f"0 as {bname}"
    if bname in classes and not classes[bname][1]:
        # a non-nullable DTO-typed property without an initializer is null in C#; keep that
        return "null as never"
    return "null as never"


unmapped_defaults = []


CLASS_RE = re.compile(
    r"\bpublic\s+(?P<mods>(?:(?:sealed|abstract|static|partial)\s+)*)(?:class|record)\s+(?P<name>\w+)"
    r"(?:\s*<(?P<generics>[^>]+)>)?(?:\s*:\s*(?P<bases>[^{]+?))?\s*(?:where\s+[^{]+)?\{")
ENUM_RE = re.compile(r"\bpublic\s+enum\s+(?P<name>\w+)(?:\s*:\s*\w+)?\s*\{")
PROP_RE = re.compile(
    r"\bpublic\s+(?P<mods>(?:(?:required|virtual|override|new|sealed)\s+)*)"
    r"(?P<type>[\w.]+(?:\s*<[^;{}()=]*?>)?(?:\[\])?\??)\s+(?P<name>\w+)\s*(?P<kind>\{|=>)")
CONST_RE = re.compile(r"\bpublic\s+const\s+string\s+(?P<name>\w+)\s*=\s*(?P<value>@?\"(?:[^\"\\]|\\.)*\")\s*;")


def read_sources(folder: str) -> list[tuple[str, str]]:
    files = []
    for dirpath, dirnames, filenames in os.walk(folder):
        dirnames[:] = [d for d in dirnames if d not in ("bin", "obj")]
        for f in sorted(filenames):
            if f.endswith(".cs"):
                path = os.path.join(dirpath, f)
                with open(path, encoding="utf-8-sig") as fh:
                    files.append((os.path.relpath(path, CORE), strip_attributes(strip_comments(fh.read()))))
    return files


def main() -> int:
    dto_files = read_sources(os.path.join(CORE, "DTOs"))
    entity_files = read_sources(os.path.join(CORE, "Entities"))

    enums, classes, consts = {}, {}, {}
    for rel, src in dto_files + entity_files:
        for m in ENUM_RE.finditer(src):
            body, _ = block_after(src, m.start())
            members, value = [], -1
            for part in [p.strip() for p in body.split(",") if p.strip()]:
                em = re.fullmatch(r"(\w+)\s*(?:=\s*(.+))?", part, re.S)
                if not em:
                    continue
                if em.group(2) is not None:
                    raw = em.group(2).strip()
                    value = int(raw, 16) if raw.lower().startswith("0x") else int(raw) if re.fullmatch(r"-?\d+", raw) else None
                    if value is None:
                        unmapped.append(f"{rel}: enum {m.group('name')}.{em.group(1)} = {raw}")
                        continue
                else:
                    value += 1
                members.append((em.group(1), value))
            enums[m.group("name")] = (rel, members)

        for m in CLASS_RE.finditer(src):
            name, mods = m.group("name"), m.group("mods") or ""
            body, _ = block_after(src, m.start())
            visible = top_level(body)
            if "static" in mods:
                found = [(c.group("name"), c.group("value")) for c in CONST_RE.finditer(visible)]
                if found:
                    consts[name] = (rel, found)
                continue
            is_dto = rel.startswith("DTOs")
            if not is_dto:
                continue
            generics = [g.strip() for g in (m.group("generics") or "").split(",") if g.strip()]
            bases = [b.strip() for b in (m.group("bases") or "").split(",") if b.strip()]
            base = next((b for b in bases if not re.fullmatch(r"I[A-Z]\w*(<.*>)?", b)), None)
            props = []
            for p in PROP_RE.finditer(visible):
                if re.search(r"\bstatic\b", visible[max(0, p.start() - 20):p.start()]):
                    continue
                if p.group("kind") == "{":
                    # a property needs a getter; skip anything that is really a nested type or method body
                    # (read from the raw body: top_level() blanks the accessor text, but keeps offsets)
                    tail = body[p.end():p.end() + 40]
                    if not re.match(r"\s*(get|init|set|private|protected|internal)\b", tail):
                        continue
                init = None
                if p.group("kind") == "{":
                    # initializer after the accessor block: `{ get; set; } = string.Empty;`
                    depth, j = 0, p.end() - 1
                    while j < len(body):
                        if body[j] == "{":
                            depth += 1
                        elif body[j] == "}":
                            depth -= 1
                            if depth == 0:
                                break
                        j += 1
                    im = re.match(r"\s*=\s*(.+?);", body[j + 1:], re.S)
                    if im:
                        init = " ".join(im.group(1).split())
                props.append((p.group("type"), p.group("name"), p.group("kind") == "=>", init))
            classes[name] = (rel, generics, base, props)

    # Framework enums the DTOs use (System.Text.Json writes them as numbers, like our own).
    enums.setdefault("DayOfWeek", ("System.DayOfWeek", [
        ("Sunday", 0), ("Monday", 1), ("Tuesday", 2), ("Wednesday", 3), ("Thursday", 4), ("Friday", 5), ("Saturday", 6)]))

    known = set(enums) | set(classes)
    lines = [
        "// <auto-generated>",
        "// Generated by scripts/generate-client-types.py from IntelliMed.Core (DTOs + enums). Do not edit by hand:",
        "// change the C# and re-run the script.",
        "// </auto-generated>",
        "/* eslint-disable */",
        "",
    ]
    for name in sorted(enums):
        rel, members = enums[name]
        lines.append(f"/** {rel.replace(os.sep, '/')} */")
        lines.append(f"export enum {name} {{")
        lines += [f"  {member} = {value}," for member, value in members]
        lines.append("}")
        lines.append("")
    for name in sorted(consts):
        rel, members = consts[name]
        lines.append(f"/** {rel.replace(os.sep, '/')} */")
        lines.append(f"export const {name} = {{")
        lines += [f"  {member}: {value.lstrip('@')}," for member, value in members]
        lines.append("} as const;")
        lines.append("")
    for name in sorted(classes):
        rel, generics, base, props = classes[name]
        gp = set(generics)
        head = f"export interface {name}{'<' + ', '.join(generics) + '>' if generics else ''}"
        if base:
            bname = base.split("<")[0]
            if bname in classes:
                head += f" extends {ts_type(base, known, gp, name)}"
            else:
                unmapped.append(f"{name}: base {base}")
        lines.append(f"/** {rel.replace(os.sep, '/')} */")
        lines.append(head + " {")
        seen = set()
        for cs_type, prop, computed, _init in props:
            key = camel(prop)
            if key in seen:
                continue
            seen.add(key)
            ro = "readonly " if computed else ""
            lines.append(f"  {ro}{key}: {ts_type(cs_type, known, gp, f'{name}.{prop}')};")
        lines.append("}")
        lines.append("")

    # Factories: `defaultX()` is what `new X()` gives in C# — initializers included — so a form starts
    # from the same values the Blazor page did. Generic classes are skipped (no sensible default for T).
    lines.append("// ---- defaults: the value `new X()` produces in C# ----")
    helpers_at = len(lines)
    for name in sorted(classes):
        rel, generics, base, props = classes[name]
        if generics:
            continue
        chain, cur = [], name
        while cur in classes:
            chain.append(cur)
            b = classes[cur][2]
            cur = b.split("<")[0] if b else None
        merged = {}
        for cls in reversed(chain):
            for cs_type, prop, computed, init in classes[cls][3]:
                merged[camel(prop)] = (cs_type, init, f"{cls}.{prop}")
        body_lines = [f"    {key}: {default_value(cs_type, init, enums, classes, where)}," for key, (cs_type, init, where) in merged.items()]
        lines.append(f"export function default{name}(): {name} {{")
        lines.append("  return {" if body_lines else "  return {} as " + name + ";")
        if body_lines:
            lines += body_lines
            lines.append(f"  }} as {name};")
        lines.append("}")
        lines.append("")

    factories = "\n".join(lines[helpers_at:])
    helpers = []
    if "today()" in factories:
        helpers.append("const today = (): string => { const d = new Date(); return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}T00:00:00`; };")
    if "now()" in factories:
        helpers.append("const now = (): string => { const d = new Date(); const p = (n: number) => String(n).padStart(2, '0'); return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`; };")
    lines[helpers_at:helpers_at] = helpers + ([""] if helpers else [])

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="\n") as fh:
        fh.write("\n".join(lines))
    print(f"wrote {os.path.relpath(OUT, ROOT)}: {len(classes)} interfaces, {len(enums)} enums, {len(consts)} constant sets")
    if unmapped_defaults:
        print("initializers with no TS equivalent (default(T) used instead):")
        for u in unmapped_defaults:
            print("  " + u)
    if unmapped:
        print("UNMAPPED (typed as unknown):")
        for u in unmapped:
            print("  " + u)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
