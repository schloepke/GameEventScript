<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Shared syntax-highlighting cases

These presentation cases are consumed by the independent C# and Swift
SyntaxHighlighter native test adapters. They are not VM/language acceptance
cases and do not require Runtime, Compiler or Conformance packages.

Each `highlight` block has source, language and expected ranges selected by text
and optional zero-based occurrence. Every UTF-16 code unit of the selected range
must have the specified kind and, when supplied, the specified scope in its stack.
Adapters additionally verify complete coverage, ANSI text preservation and equality
of full-document and incremental-line results, including every scope and offset.

```highlight
{"name":"declarations and dispatch","language":"ges","source":"module bot.demo\non Main(args) { let result be 12; emit Done(result) }","expect":[{"text":"module","kind":"keyword"},{"text":"bot.demo","kind":"module"},{"text":"on","kind":"keyword"},{"text":"Main","kind":"message"},{"text":"args","kind":"identifier"},{"text":"let","kind":"keyword"},{"text":"result","kind":"identifier","scope":"variable.other.local.gameeventscript"},{"text":"12","kind":"number"},{"text":"emit","kind":"keyword"},{"text":"Done","kind":"message"}]}
```

```highlight
{"name":"strings quotes and comments","language":"ges","source":"'a''emit 42' \"b\"\"#ready\" // emit 12\nlet x be 7","expect":[{"text":"'a''emit 42'","kind":"string"},{"text":"''","kind":"string","scope":"constant.character.escape.quote.gameeventscript"},{"text":"\"b\"\"#ready\"","kind":"string"},{"text":"// emit 12","kind":"comment"},{"text":"let","kind":"keyword"},{"text":"7","kind":"number"}]}
```

```highlight
{"name":"unfinished multiline string unicode","language":"ges","source":"emit Out(\"😀é\r\nemit Nothing\nlast","expect":[{"text":"emit","kind":"keyword"},{"text":"\"😀é\r\nemit Nothing\nlast","kind":"string"}]}
```

```highlight
{"name":"units types and constants","language":"ges","source":"constant $speed be 12m\nlet amount be 10%\nlet roll be 2d6\nlet x be :Number(42)\nemit Done(#ready, $speed)","expect":[{"text":"$speed","kind":"constant"},{"text":"12m","kind":"number"},{"text":"10%","kind":"number"},{"text":"2d6","kind":"number"},{"text":":Number","kind":"type"},{"text":"#ready","kind":"tag"}]}
```

```highlight
{"name":"new syntax and selectors","language":"ges","source":"if let x be value; x has value { emit after 0.2s Done(x) }\nlet result be values[:fold acc be 0, item => acc + item]\nlet parts be text[:split on whitespace]","expect":[{"text":"if","kind":"keyword"},{"text":"let","kind":"keyword"},{"text":"after","kind":"keyword"},{"text":"0.2s","kind":"number"},{"text":":fold","kind":"builtin"},{"text":":split","kind":"builtin"},{"text":"=>","kind":"keyword"}]}
```

```highlight
{"name":"zero-width end and embedded block","language":"gesa","source":".segment source \"bot.ges\"\nlet x be 42\n.region-end \"source\"\n.segment code\nLoadInteger r1, #42\n","expect":[{"text":".segment","kind":"keyword"},{"text":"let","kind":"keyword"},{"text":"42","kind":"number"},{"text":"LoadInteger","kind":"keyword"},{"text":"r1","kind":"register"},{"text":"#42","kind":"number"}]}
```

```highlight
{"name":"embedded boundary inside multiline string","language":"gesa","source":".segment source \"x\"\nemit Done('😀\n.segment code\n.region-end \"source\"\nstill a string')\n.region-end \"source\"\nReturnVoid\n","expect":[{"text":"emit","kind":"keyword"},{"text":"'😀\n.segment code\n.region-end \"source\"\nstill a string'","kind":"string"},{"text":"ReturnVoid","kind":"keyword"}]}
```

```highlight
{"name":"single embedded source line CRLF","language":"gesa","source":".source-line \"file.ges\" 3 | emit Done('r2 // quoted', 12) // r3\r\nL_0: LoadInteger r1, #42\r\n","expect":[{"text":"emit","kind":"keyword"},{"text":"'r2 // quoted'","kind":"string"},{"text":"// r3","kind":"comment"},{"text":"LoadInteger","kind":"keyword"},{"text":"r1","kind":"register"},{"text":"#42","kind":"number"}]}
```

```highlight
{"name":"assembler JSON escapes","language":"gesa","source":".module \"😀\\\"name\\\\path\"\nLoadFloat r0, #1e-5\n","expect":[{"text":"\"😀\\\"name\\\\path\"","kind":"string"},{"text":"LoadFloat","kind":"keyword"},{"text":"#1e-5","kind":"number"}]}
```

```highlight
{"name":"anchors and unicode offsets","language":"ges","source":"😀\tlet x be \"é\"\r// café\rlet y be 2","expect":[{"text":"let","kind":"keyword"},{"text":"x","kind":"identifier"},{"text":"\"é\"","kind":"string"},{"text":"// café","kind":"comment"},{"text":"2","kind":"number"}]}
```

```highlight
{"name":"function predicate labels and extension","language":"ges","source":"function double(_ x as :Number) be x + x\npredicate ready(value) be value has value\nlet x be :math.distance(x: 1, y: 2)","expect":[{"text":"double","kind":"function"},{"text":"ready","kind":"function"},{"text":":Number","kind":"type"},{"text":"1","kind":"number"},{"text":"2","kind":"number"}]}
```

```highlight
{"name":"unfinished strings in source-line stop at line boundary","language":"gesa","source":".source-line \"x\" 1 | emit Done('open\nReturnVoid\n.source-line \"x\" 2 | emit Done(\"open\r\nLoadInteger r1, #42\n","expect":[{"text":"'open","kind":"string"},{"text":"ReturnVoid","kind":"keyword"},{"text":"\"open","kind":"string"},{"text":"LoadInteger","kind":"keyword"},{"text":"#42","kind":"number"}]}
```

```highlight
{"name":"ges comments retain Unicode separators","language":"ges","source":"// a\u0085b\u2028c\u2029d\u000be\ff emit Hidden()\nemit Done()","expect":[{"text":"// a\u0085b\u2028c\u2029d\u000be\ff emit Hidden()","kind":"comment"},{"text":"emit","occurrence":1,"kind":"keyword"}]}
```

```highlight
{"name":"gesa comments retain Unicode separators","language":"gesa","source":"// a\u0085b\u2028c\u2029d\u000be\ff emit Hidden()\nReturnVoid","expect":[{"text":"// a\u0085b\u2028c\u2029d\u000be\ff emit Hidden()","kind":"comment"},{"text":"ReturnVoid","occurrence":0,"kind":"keyword"}]}
```

```highlight
{"name":"source-line double string retains Unicode separators","language":"gesa","source":".source-line \"bot.ges\" 1 | emit Done(\"a\u0085b\u2028c\u2029d\u000be\ff\")\r\nReturnVoid\n","expect":[{"text":"\"a\u0085b\u2028c\u2029d\u000be\ff\"","kind":"string"},{"text":"ReturnVoid","kind":"keyword"}]}
```

```highlight
{"name":"source-line single string retains Unicode separators","language":"gesa","source":".source-line \"bot.ges\" 1 | emit Done('a\u0085b\u2028c\u2029d\u000be\ff')\r\nReturnVoid\n","expect":[{"text":"'a\u0085b\u2028c\u2029d\u000be\ff'","kind":"string"},{"text":"ReturnVoid","kind":"keyword"}]}
```

```highlight
{"name":"multiline handler declaration","language":"ges","source":"module review.test\non\nMain(args) { emit Done(args) }","expect":[{"text":"on","kind":"keyword","scope":"keyword.control.handler.gameeventscript"},{"text":"Main","kind":"message"},{"text":"emit","kind":"keyword"}]}
```

```highlight
{"name":"standalone declaration keywords while editing","language":"ges","source":"module\non\rlet\r\nrecord\nfunction\npredicate\nconstant","expect":[{"text":"module","kind":"keyword","scope":"keyword.control.module.gameeventscript"},{"text":"on","kind":"keyword","scope":"keyword.control.handler.gameeventscript"},{"text":"let","kind":"keyword","scope":"keyword.other.definition.gameeventscript"},{"text":"record","kind":"keyword","scope":"keyword.other.definition.gameeventscript"},{"text":"function","kind":"keyword","scope":"keyword.other.definition.gameeventscript"},{"text":"predicate","kind":"keyword","scope":"keyword.other.definition.gameeventscript"},{"text":"constant","kind":"keyword","scope":"keyword.other.definition.gameeventscript"}]}
```
