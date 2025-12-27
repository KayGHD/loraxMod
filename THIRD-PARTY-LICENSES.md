# Third-Party Licenses

LoraxMod uses the following open-source projects. We are grateful to their authors and contributors.

## Core Dependencies

### tree-sitter

- **Repository:** https://github.com/tree-sitter/tree-sitter
- **License:** MIT
- **Copyright:** (c) 2018 Max Brunsfeld

The tree-sitter parsing library that powers all language parsing in LoraxMod.

### TreeSitter.DotNet (C# binding)

- **Package:** https://www.nuget.org/packages/TreeSitter.DotNet
- **Repository:** https://github.com/AlyRafiSR/TreeSitter.DotNet
- **License:** MIT
- **Maintainer:** mariusgreuel

.NET bindings for tree-sitter with native language parsers.

### tree-sitter-language-pack (Python binding)

- **Package:** https://pypi.org/project/tree-sitter-language-pack/
- **Repository:** https://github.com/AWhetter/tree-sitter-language-pack
- **License:** MIT OR Apache-2.0 (dual license)

Pre-built tree-sitter parsers for 170+ languages.

### py-tree-sitter

- **Package:** https://pypi.org/project/tree-sitter/
- **Repository:** https://github.com/tree-sitter/py-tree-sitter
- **License:** MIT
- **Copyright:** (c) 2019 Max Brunsfeld

Python bindings for tree-sitter.

## Grammar Repositories

All grammar repositories are cloned to `grammars/tree-sitter-*/` for schema access (node-types.json).

### tree-sitter-bash
- **Repository:** https://github.com/tree-sitter/tree-sitter-bash
- **License:** MIT
- **Copyright:** (c) 2017 Max Brunsfeld

### tree-sitter-c
- **Repository:** https://github.com/tree-sitter/tree-sitter-c
- **License:** MIT
- **Copyright:** (c) 2014 Max Brunsfeld

### tree-sitter-cpp
- **Repository:** https://github.com/tree-sitter/tree-sitter-cpp
- **License:** MIT
- **Copyright:** (c) 2014 Max Brunsfeld

### tree-sitter-c-sharp
- **Repository:** https://github.com/tree-sitter/tree-sitter-c-sharp
- **License:** MIT
- **Copyright:** (c) 2014-2023 Max Brunsfeld, Damien Guard, Amaan Qureshi, and contributors

### tree-sitter-css
- **Repository:** https://github.com/tree-sitter/tree-sitter-css
- **License:** MIT
- **Copyright:** (c) 2018 Max Brunsfeld

### tree-sitter-fortran
- **Repository:** https://github.com/stadelmanma/tree-sitter-fortran
- **License:** MIT

### tree-sitter-html
- **Repository:** https://github.com/tree-sitter/tree-sitter-html
- **License:** MIT
- **Copyright:** (c) 2014 Max Brunsfeld

### tree-sitter-javascript
- **Repository:** https://github.com/tree-sitter/tree-sitter-javascript
- **License:** MIT
- **Copyright:** (c) 2014 Max Brunsfeld

### tree-sitter-powershell
- **Repository:** https://github.com/airbus-cert/tree-sitter-powershell
- **License:** MIT
- **Copyright:** (c) 2023 Airbus CERT

### tree-sitter-python
- **Repository:** https://github.com/tree-sitter/tree-sitter-python
- **License:** MIT
- **Copyright:** (c) 2016 Max Brunsfeld

### tree-sitter-r
- **Repository:** https://github.com/r-lib/tree-sitter-r
- **License:** MIT
- **Copyright:** (c) 2025 tree-sitter-r authors

### tree-sitter-rust
- **Repository:** https://github.com/tree-sitter/tree-sitter-rust
- **License:** MIT
- **Copyright:** (c) 2017 Maxim Sokolov

## Additional Languages (via tree-sitter-language-pack)

The Python binding (`loraxmod`) supports 170+ languages through tree-sitter-language-pack. All bundled languages use permissive licenses (MIT, Apache-2.0, BSD variants, Unlicense). No GPL-licensed languages are included.

See: https://github.com/AWhetter/tree-sitter-language-pack for the complete list.

## Additional Languages (via TreeSitter.DotNet)

The C# binding supports 28 languages through TreeSitter.DotNet. All use MIT license.

Languages: bash, c, cpp, csharp, css, go, html, java, javascript, json, python, rust, typescript, tsx, php, ruby, swift, scala, haskell, julia, ocaml, agda, toml, jsdoc, ql, tsq, embedded-template, verilog

## License Compatibility

All dependencies use permissive licenses (MIT, Apache-2.0) compatible with LoraxMod's MIT license.

- **MIT License:** Allows commercial use, modification, distribution, and private use with attribution
- **Apache-2.0:** Similar to MIT with additional patent provisions

Users of LoraxMod must include this THIRD-PARTY-LICENSES.md file or equivalent attribution when redistributing the software.
