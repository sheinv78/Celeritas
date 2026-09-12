#!/usr/bin/env bash
# Compiles every C# block in README.md and docs/**/*.md against the library in this repository.
#
# Nothing else compiles them, so a block can drift into quoting a version of the library that no
# longer exists — a renamed member, a removed overload, a record whose shape changed — and a
# reader has no way to tell such a block from a current one. examples/ had exactly that problem
# until scripts/check-examples.sh was written; this is the same check for the prose.
#
# The blocks are compiled, not run. What a block prints — a key, a confidence, a chord name — is
# a claim about the library's behaviour, and examples/ already makes those claims where they can
# be checked by running (check-examples.sh compares each program with its Expected Output block).
# Here the question is only whether the code a reader would copy still exists.
#
# How a block becomes a compilation unit:
#   - `using` directives anywhere in the block are hoisted to the top of the file;
#   - a block that opens with a namespace, class, struct, record, interface or enum is compiled
#     as it is, inside a namespace of its own;
#   - a block whose lines are member declarations (a `private static readonly` field) is wrapped
#     in a class;
#   - any other block is a method body and is wrapped in a static method.
#   The project opens every hand-written Celeritas namespace (not the ANTLR-generated
#   Celeritas.Core.Grammar), because the documents open them once at the top or when a recipe
#   first reaches for one, and a block is not made to repeat them. A `using` a
#   block does write is still checked: a namespace that does not exist is an error.
#
# Two directives, written as an HTML comment on the line above the fence, tell the script about
# a block. Markdown does not render the comment, so the page is unchanged:
#   <!-- snippet: no-compile <reason> -->   the block is prose in the shape of code (a signature,
#                                           a sketch); it is skipped, counted, and listed, so
#                                           that what is not checked stays visible;
#   <!-- snippet: given <parameters> -->    the names the block uses without declaring them,
#                                           typed, as the parameter list of the wrapping method:
#                                           `given NoteBuffer buffer, Stream stream`.
# `no-compile` may also be written on the fence itself: ```csharp no-compile
#
# The blocks are compiled together, and each error is reported against the document and line it
# sits on. A block with a syntax error stops the compiler before it binds the others, and a block
# that was never bound has not been checked — so when a build fails, the blocks it named are set
# aside and the rest are built again, until a build succeeds. A block is reported ok only when it
# was part of a build that did.

set -euo pipefail

repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

# MSBuild reads the generated project file, and the compiler prints the paths the #line
# directives carry; under Git Bash on Windows neither can follow a POSIX path such as /c/Users/...
# Translate when the tool for it is there; elsewhere the path is already the one they want.
if command -v cygpath >/dev/null 2>&1; then
    repo_for_msbuild="$(cygpath -m "$repo")"
else
    repo_for_msbuild="$repo"
fi

mkdir -p "$work/src"
cat > "$work/snippets.csproj" <<PROJECT
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <!-- A library: nothing here is run, and a library needs no entry point, so two blocks that
         both declare a Main do not collide. -->
    <OutputType>Library</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <InvariantGlobalization>true</InvariantGlobalization>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
    <!-- A document that teaches an obsolete member is the same drift as one that names a
         member that is gone, so the obsolete warnings fail the check. -->
    <WarningsAsErrors>CS0612;CS0618</WarningsAsErrors>
    <EnforceCodeStyleInBuild>false</EnforceCodeStyleInBuild>
  </PropertyGroup>
  <ItemGroup>
    <Using Include="Celeritas.Core" />
    <Using Include="Celeritas.Core.Accompaniment" />
    <Using Include="Celeritas.Core.Analysis" />
    <Using Include="Celeritas.Core.FiguredBass" />
    <Using Include="Celeritas.Core.Harmonization" />
    <Using Include="Celeritas.Core.Midi" />
    <Using Include="Celeritas.Core.Notation" />
    <Using Include="Celeritas.Core.Orchestration" />
    <Using Include="Celeritas.Core.Ornamentation" />
    <Using Include="Celeritas.Core.Simd" />
    <Using Include="Celeritas.Core.VoiceLeading" />
  </ItemGroup>
  <ItemGroup>
    <Compile Include="src/*.cs" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="$repo_for_msbuild/src/Celeritas/Celeritas.csproj" />
  </ItemGroup>
</Project>
PROJECT

# Every document with C# in it. README.md first, then docs/ in path order, so the report reads in
# the order a person would.
documents=("$repo/README.md")
while IFS= read -r document; do
    documents+=("$document")
done < <(find "$repo/docs" -name '*.md' | LC_ALL=C sort)

# Extracts the blocks. For each one it writes src/snippet-NNN.cs and appends a line to index.tsv:
#   id <TAB> document (relative to the repo) <TAB> fence line <TAB> first code line <TAB> last code line <TAB> kind <TAB> note
# where kind is compiled or skipped, and note is the no-compile reason.
awk -v repo="$repo" -v repo_for_msbuild="$repo_for_msbuild" -v work="$work" '
function trim(s) { sub(/^[[:space:]]+/, "", s); sub(/[[:space:]]+$/, "", s); return s }

function is_using_directive(line) {
    # `using Celeritas.Core;`, `using static X.Y;`, `using Alias = X.Y;`, with or without a trailing
    # comment — but not `using var x = ...` nor `using (var x = ...)`, which are statements and stay
    # where they are.
    return line ~ /^[[:space:]]*(global[[:space:]]+)?using[[:space:]]+(static[[:space:]]+)?[A-Za-z_][A-Za-z0-9_.]*([[:space:]]*=[[:space:]]*[A-Za-z_][A-Za-z0-9_.<>, ]*)?[[:space:]]*;[[:space:]]*(\/\/.*)?$/
}

function opens_a_type(line) {
    return line ~ /^[[:space:]]*((public|internal|private|protected|static|sealed|abstract|partial|readonly|unsafe|file)[[:space:]]+)*(class|struct|record|interface|enum|namespace)[[:space:]]+[A-Za-z_]/
}

function is_member_declaration(line) {
    return line ~ /^[[:space:]]*(public|private|protected|internal)[[:space:]]/
}

function write_snippet(    id, path, out, i, line, kind, usings) {
    count++
    id = sprintf("%03d", count)
    path = FILENAME
    if (index(path, repo "/") == 1) path = substr(path, length(repo) + 2)

    if (skip != "") {
        printf "%s\t%s\t%d\t%d\t%d\tskipped\t%s\n", id, path, fence_line, fence_line + 1, fence_line + n, skip >> (work "/index.tsv")
        return
    }

    # Hoist the using directives, each under a #line of its own so an error in one is reported
    # where it stands in the document; each leaves an empty line behind so the numbering holds.
    usings = ""
    kind = ""
    for (i = 1; i <= n; i++) {
        line = block[i]
        if (is_using_directive(line)) {
            usings = usings sprintf("#line %d \"%s/%s\"\n%s\n", fence_line + i, repo_for_msbuild, path, trim(line))
            block[i] = ""
            continue
        }
        if (kind == "" && trim(line) != "" && trim(line) !~ /^\/\//) {
            if (opens_a_type(line)) kind = "program"
            else if (is_member_declaration(line)) kind = "members"
            else kind = "statements"
        }
    }
    if (kind == "") kind = "statements"

    out = work "/src/snippet-" id ".cs"
    printf "%s", usings > out
    if (usings != "") printf "#line default\n" > out
    printf "namespace Snippet_%s\n{\n", id > out
    if (kind != "program") printf "class Snippet\n{\n" > out
    if (kind == "statements") printf "static void Run(%s)\n{\n", given > out
    printf "#line %d \"%s/%s\"\n", fence_line + 1, repo_for_msbuild, path > out
    for (i = 1; i <= n; i++) printf "%s\n", block[i] > out
    printf "#line default\n" > out
    if (kind == "statements") printf "}\n" > out
    if (kind != "program") printf "}\n" > out
    printf "}\n" > out
    close(out)

    printf "%s\t%s\t%d\t%d\t%d\tcompiled\t%s\n", id, path, fence_line, fence_line + 1, fence_line + n, kind >> (work "/index.tsv")
}

function unterminated_fence() {
    path = FILENAME
    if (index(path, repo "/") == 1) path = substr(path, length(repo) + 2)
    printf "%s:%d: this C# fence is never closed\n", path, fence_line > "/dev/stderr"
    unknown++
}

FNR == 1 { if (in_fence == 1) unterminated_fence(); in_fence = 0; previous = "" }

{ sub(/\r$/, "") }

in_fence == 1 {
    if (trim($0) == "```") { in_fence = 0; write_snippet(); previous = ""; next }
    block[++n] = $0
    next
}

in_fence == 2 {
    if (trim($0) == "```") in_fence = 0
    next
}

/^[[:space:]]*```/ {
    info = trim($0); sub(/^```/, "", info); info = trim(info)
    if (info ~ /^(csharp|cs)([[:space:]]|$)/) {
        in_fence = 1
        fence_line = FNR
        n = 0
        skip = ""
        given = ""
        if (info ~ /[[:space:]]no-compile([[:space:]]|$)/) skip = "no-compile on the fence"
        if (previous ~ /^<!--[[:space:]]*snippet:/) {
            directive = previous
            sub(/^<!--[[:space:]]*snippet:[[:space:]]*/, "", directive)
            sub(/[[:space:]]*-->[[:space:]]*$/, "", directive)
            if (directive ~ /^no-compile([[:space:]]|$)/) {
                skip = trim(substr(directive, length("no-compile") + 1))
                if (skip == "") skip = "no reason given"
            } else if (directive ~ /^given[[:space:]]/) {
                given = trim(substr(directive, length("given") + 1))
            } else {
                path = FILENAME
                if (index(path, repo "/") == 1) path = substr(path, length(repo) + 2)
                printf "%s:%d: unknown snippet directive \"%s\"; the script knows no-compile and given\n", path, FNR - 1, directive > "/dev/stderr"
                unknown++
            }
        }
    } else {
        in_fence = 2
    }
    next
}

trim($0) != "" { previous = trim($0) }

END {
    if (in_fence == 1) unterminated_fence()
    if (unknown > 0) exit 2
}
' "${documents[@]}"

if [[ ! -f "$work/index.tsv" ]]; then
    echo "no C# blocks found under $repo"
    exit 1
fi

compiled=0
skipped=0
while IFS=$'\t' read -r _ _ _ _ _ kind _; do
    if [[ "$kind" == "skipped" ]]; then skipped=$((skipped + 1)); else compiled=$((compiled + 1)); fi
done < "$work/index.tsv"

# Files every error line, once, under the block it belongs to, and takes that block out of the
# next build. The compiler reports a block's error at the document and line the #line directive
# named; one at the wrapper's closing braces (an unclosed brace, a missing semicolon on the last
# line) falls to the generated file, and is reported against the block all the same. Returns
# the number of blocks set aside.
mkdir -p "$work/errors"
set_aside_the_failing_blocks() {
    local line location message file position row id relative where
    local candidate document first last kind
    local set_aside=0
    while IFS= read -r line; do
        [[ -z "$line" ]] && continue
        location="${line%%: error *}"
        message="${line#*: error }"
        message="${message% \[*\]}"
        file="${location%(*}"
        file="${file//\\//}"
        position="${location##*(}"
        position="${position%)}"
        row="${position%%,*}"
        [[ "$row" =~ ^[0-9]+$ ]] || row=0

        id=""
        if [[ "$file" == *"/src/snippet-"*".cs" ]]; then
            id="${file##*/snippet-}"
            id="${id%.cs}"
            where="at the end of the block"
        else
            relative="${file#"$repo_for_msbuild"/}"
            while IFS=$'\t' read -r candidate document _ first last kind _; do
                [[ "$kind" == "compiled" ]] || continue
                if [[ "$document" == "$relative" && "$row" -ge "$first" && "$row" -le "$last" ]]; then
                    id="$candidate"
                    break
                fi
            done < "$work/index.tsv"
            where="line $row"
        fi

        if [[ -n "$id" ]]; then
            echo "           $where: error $message" >> "$work/errors/$id"
            if [[ -f "$work/src/snippet-$id.cs" ]]; then
                rm "$work/src/snippet-$id.cs"
                set_aside=$((set_aside + 1))
            fi
        else
            echo "           $line" >> "$work/errors/unplaced"
        fi
    done < <(printf '%s\n' "$1" | grep -E ': error ' | sort -u || true)
    echo "$set_aside"
}

built=0
for _ in 1 2 3 4 5 6 7 8 9 10; do
    if build_log="$(cd "$work" && dotnet build -nologo -v q 2>&1)"; then
        built=1
        break
    fi
    # A round that names no block has hit something other than a block — a restore failure, a
    # project error — and another round would only hit it again.
    if [[ "$(set_aside_the_failing_blocks "$build_log")" -eq 0 ]]; then
        break
    fi
done

failed=0
unchecked=0
while IFS=$'\t' read -r id document fence _ _ kind note; do
    if [[ "$kind" == "skipped" ]]; then
        echo "skipped  $document:$fence ($note)"
    elif [[ -f "$work/errors/$id" ]]; then
        echo "FAILED   $document:$fence does not compile against the library"
        cat "$work/errors/$id"
        failed=$((failed + 1))
    elif [[ "$built" -eq 1 ]]; then
        echo "ok       $document:$fence"
    else
        echo "-        $document:$fence was not checked: no build finished"
        unchecked=$((unchecked + 1))
    fi
done < "$work/index.tsv"

echo
if [[ "$built" -eq 0 && "$failed" -eq 0 && ! -f "$work/errors/unplaced" ]]; then
    echo "the build failed without naming a block; the last lines of its log:"
    printf '%s\n' "$build_log" | tail -20 | sed 's/^/           /'
    exit 1
fi

if [[ -f "$work/errors/unplaced" ]]; then
    echo "errors that could not be placed on a block:"
    cat "$work/errors/unplaced"
    failed=$((failed + 1))
fi

if [[ "$unchecked" -gt 0 ]]; then
    echo "$unchecked of $compiled blocks were not checked, because no build finished."
    failed=$((failed + 1))
fi

if [[ "$skipped" -gt 0 ]]; then
    echo "$skipped of $((compiled + skipped)) blocks are marked no-compile and were not checked."
fi

if [[ "$failed" -gt 0 ]]; then
    echo "$failed of $compiled blocks do not compile against the library."
    exit 1
fi

echo "all $compiled blocks compile against the library."
