# Third-party notices

RimKit includes or works with the software below. Each part stays under its own license, which is not changed by the RimKit License.

## Included in the source

| Component | Where | License | Copyright |
|-----------|-------|---------|-----------|
| Luau (the VM and the compiler) | `src/native/third_party/luau` | MIT, full text in `src/native/third_party/luau/LICENSE.txt` | Roblox Corporation, and Lua.org, PUC-Rio for the Lua parts |
| sol2 (C++ to Lua binding headers) | `src/native/third_party/sol2` | MIT, full text in `src/native/third_party/sol2/LICENSE.txt` | Rapptz, ThePhD and contributors |

## Used by the documentation site

These are installed with `npm install` in `infrastructure/docs-site` and are not part of the mod: marked (MIT), sanitize-html (MIT), highlight.js (BSD-3-Clause), mermaid (MIT).

## Needed at run time, not included

| Component | License | Author |
|-----------|---------|--------|
| Harmony | MIT | Andreas Pardeike |
| RimWorld | Proprietary | Ludeon Studios |
| Luau Language Server (editor) | MIT | JohnnyMorganz |

RimKit does not ship RimWorld or Harmony. Players install them themselves.

## Full license texts

These two are compiled into `rimlua_core.dll` and `rimkit.exe`, so their notices travel with every build of RimKit.

### Luau

```text
MIT License

Copyright (c) 2019-2026 Roblox Corporation
Copyright (c) 1994–2019 Lua.org, PUC-Rio.

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
of the Software, and to permit persons to whom the Software is furnished to do
so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

### sol2

```text
The MIT License (MIT)

Copyright (c) 2013-2022 Rapptz, ThePhD, and contributors

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
```
