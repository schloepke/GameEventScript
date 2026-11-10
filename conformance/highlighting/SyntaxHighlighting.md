<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Shared syntax-highlighting cases

These presentation cases are consumed by the independent C# and Swift
Syntax native test adapters. They are not VM/language acceptance
cases and do not require Runtime, Compiler or Conformance packages.

Each `highlight` block has source, language and expected ranges selected by text
and optional zero-based occurrence. Every UTF-16 code unit of the selected range
must have the specified kind. `diagnostics`, when present, lists every diagnostic
of the result in order: its code and the range it covers, selected the same way;
an optional `length` replaces the selected text's length, such as 0 for a position,
and `after` selects the position after the text.
Cases without the key do not check diagnostics; most are fragments, not programs. Adapters additionally verify
complete coverage, that each span lists exactly the diagnostics overlapping it,
and ANSI text preservation.

```highlight
{"name":"declarations and dispatch","language":"ges","source":"module bot.demo\non Main(args) { let result be 12; emit Done(result) }","expect":[{"text":"module","kind":"keyword"},{"text":"bot.demo","kind":"module"},{"text":"on","kind":"keyword"},{"text":"Main","kind":"message"},{"text":"args","kind":"identifier"},{"text":"let","kind":"keyword"},{"text":"result","kind":"identifier"},{"text":"12","kind":"number"},{"text":"emit","kind":"keyword"},{"text":"Done","kind":"message"}]}
```

```highlight
{"name":"strings quotes and comments","language":"ges","source":"'a''emit 42' \"b\"\"#ready\" // emit 12\nlet x be 7","expect":[{"text":"'a''emit 42'","kind":"string"},{"text":"''","kind":"string"},{"text":"\"b\"\"#ready\"","kind":"string"},{"text":"// emit 12","kind":"comment"},{"text":"let","kind":"keyword"},{"text":"7","kind":"number"}]}
```

```highlight
{"name":"unfinished multiline string unicode","language":"ges","source":"emit Out(\"😀é\r\nemit Nothing\nlast","expect":[{"text":"emit","kind":"keyword"},{"text":"\"😀é\r\nemit Nothing\nlast","kind":"string"}]}
```

```highlight
{"name":"units types and constants","language":"ges","source":"constant $speed be 12m\nlet amount be 10%\nlet dice be roll dice 2d6\nlet x be :Number(42)\nemit Done(#ready, $speed)","expect":[{"text":"$speed","kind":"constant"},{"text":"12m","kind":"number"},{"text":"10%","kind":"number"},{"text":"2d6","kind":"number"},{"text":":Number","kind":"type"},{"text":"#ready","kind":"tag"}]}
```

```highlight
{"name":"physical unit literals and quantity types","language":"ges","source":"let speed be 10m/s\nlet split be 10m / s\nlet delay be 250ms\nlet turn be 120deg/s\nlet x be 5 as :Quantity(m/s^2)\nlet f be 21400N\nlet gap be 10mm\nlet energy be 1.5kWh\nlet push be 6m/s²\nlet rate be 5 as :Quantity(Hz)","expect":[{"text":"10m/s","kind":"number"},{"text":"10m","kind":"number","occurrence":1},{"text":"250ms","kind":"number"},{"text":"120deg/s","kind":"number"},{"text":"Quantity","kind":"type"},{"text":"m/s^2","kind":"builtin"},{"text":"21400N","kind":"number"},{"text":"10mm","kind":"number"},{"text":"1.5kWh","kind":"number"},{"text":"6m/s²","kind":"number"},{"text":"Quantity","kind":"type","occurrence":1},{"text":"Hz","kind":"builtin"}]}
```

```highlight
{"name":"thermal unit literals and quantity types","language":"ges","source":"let g be 3W/K\nlet m be 250mW/K\nlet c be 2kJ/K\nlet split be 10W / K\nlet x be 5 as :Quantity(J/K)","expect":[{"text":"3W/K","kind":"number"},{"text":"250mW/K","kind":"number"},{"text":"2kJ/K","kind":"number"},{"text":"10W","kind":"number"},{"text":"J/K","kind":"builtin","occurrence":1}]}
```

```highlight
{"name":"area volume flux level and rate literals","language":"ges","source":"let area be 2m²\nlet sq be 2m^2\nlet vol be 0.5m³\nlet flux be 14mW/m²\nlet level be 6dB\nlet mass be 2t\nlet split be 4 / s\nlet rate be 4/s\nlet power be (2m)²\nlet x be 5 as :Quantity(W/m^2)","expect":[{"text":"2m²","kind":"number"},{"text":"2m^2","kind":"number"},{"text":"0.5m³","kind":"number"},{"text":"14mW/m²","kind":"number"},{"text":"6dB","kind":"number"},{"text":"2t","kind":"number"},{"text":"4","kind":"number","occurrence":1},{"text":"s","kind":"identifier","occurrence":4},{"text":"4/s","kind":"number"},{"text":"2m","kind":"number","occurrence":2},{"text":"²","kind":"keyword","occurrence":2},{"text":"W/m^2","kind":"builtin"}]}
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
{"name":"multiline handler declaration","language":"ges","source":"module review.test\non\nMain(args) { emit Done(args) }","expect":[{"text":"on","kind":"keyword"},{"text":"Main","kind":"message"},{"text":"emit","kind":"keyword"}]}
```

```highlight
{"name":"standalone declaration keywords while editing","language":"ges","source":"module\non\rlet\r\nrecord\nfunction\npredicate\nconstant","expect":[{"text":"module","kind":"keyword"},{"text":"on","kind":"keyword"},{"text":"let","kind":"keyword"},{"text":"record","kind":"keyword"},{"text":"function","kind":"keyword"},{"text":"predicate","kind":"keyword"},{"text":"constant","kind":"keyword"}]}
```

```highlight
{"name":"postfix chains after parenthesized extensions","language":"ges","source":":board.top(zone: #discard).id\n:board.cards(zone: #draw)[:filter card where card.id > 1][:count]","expect":[{"text":"id","kind":"identifier"},{"text":"#discard","kind":"tag"},{"text":":filter","kind":"builtin"},{"text":":count","kind":"builtin"}]}
```

```highlight
{"name":"parameterless predicate shorthand","language":"ges","source":"if is drawable and is not blocked { emit Ready() }","expect":[{"text":"is","kind":"keyword"},{"text":"not","kind":"keyword"},{"text":"drawable","kind":"function"},{"text":"blocked","kind":"function"}]}
```

```highlight
{"name":"inline assembly registers and instructions","language":"ges","source":"let result be asm {\n    .register temp\n    LoadInteger temp, 7\n    Multiply result, temp, temp // preserve instructions\n}\nemit Done(value: result)","expect":[{"text":"asm","kind":"keyword"},{"text":".register","kind":"keyword"},{"text":"LoadInteger","kind":"builtin"},{"text":"Multiply","kind":"builtin"},{"text":"7","kind":"number"},{"text":"// preserve instructions","kind":"comment"},{"text":"emit","kind":"keyword"}]}
```

```highlight
{"name":"inclusive between comparisons","language":"ges","source":"if x is between 1 and 10 or x is not between 20 and 30 {}","expect":[{"text":"is","occurrence":0,"kind":"keyword"},{"text":"between","occurrence":0,"kind":"keyword"},{"text":"and","occurrence":0,"kind":"keyword"},{"text":"not","kind":"keyword"},{"text":"between","occurrence":1,"kind":"keyword"}]}
```

```highlight
{"name":"combined pipeline selectors","language":"ges","source":"[:cartesian a, b :filter x, y where x = y :select x, y => x][:foreach value => :ext.save(value)]","expect":[{"text":":cartesian","kind":"builtin"},{"text":":filter","kind":"builtin"},{"text":":select","kind":"builtin"},{"text":":foreach","kind":"builtin"}]}
```

```highlight
{"name":"condition-first guarded choice","language":"ges","source":"let rank be\n  when value = 14 then 'A',\n  when value = 13 then 'K'\n  otherwise value as :Text","expect":[{"text":"when","kind":"keyword"},{"text":"then","kind":"keyword"},{"text":"when","occurrence":1,"kind":"keyword"},{"text":"then","occurrence":1,"kind":"keyword"},{"text":"otherwise","kind":"keyword"}]}
```

```highlight
{"name":"strict literal parsing","language":"ges","source":"let a be parse strict input\nlet b be parse input\nlet strict be 1","expect":[{"text":"parse","occurrence":0,"kind":"keyword"},{"text":"strict","occurrence":0,"kind":"keyword"},{"text":"input","occurrence":0,"kind":"identifier"},{"text":"parse","occurrence":1,"kind":"keyword"},{"text":"strict","occurrence":1,"kind":"identifier"}]}
```

```highlight
{"name":"syntax error diagnostic","language":"ges","source":"on Start() {\n  let x be\n}\nemit Done()","expect":[{"text":"let","kind":"keyword"},{"text":"emit","kind":"keyword"}],"diagnostics":[{"code":"parse.syntax","text":"}"},{"code":"parse.syntax","text":"emit"}]}
```

```highlight
{"name":"single-source rule diagnostics","language":"ges","source":"on Start(a, a) {\n  let total be 1\n  let total be 2\n}","expect":[{"text":"total","occurrence":1,"kind":"identifier"}],"diagnostics":[{"code":"validate.duplicateHandlerParameter","text":"a","occurrence":2},{"code":"validate.duplicateVariable","text":"let total be 2"}]}
```

```highlight
{"name":"unresolved variable diagnostics","language":"ges","source":"on Boot(bot) { publish Probe(value: x) }\non Start(items) { if let first be items; first > 1 { emit Found(value: first) }; emit Gone(value: first) }","expect":[{"text":"x","kind":"identifier"},{"text":"first","occurrence":3,"kind":"identifier"}],"diagnostics":[{"code":"validate.unresolvedVariable","text":"x"},{"code":"validate.unresolvedVariable","text":"first","occurrence":3}]}
```

```highlight
{"name":"bindings and later declarations resolve","language":"ges","source":"on Start(items) {\n  let best be items[:max s => s.level]\n  if let first be best; first > $limit { emit Found(value: double(first)) }\n  for item in items and copy in [item] emit Seen(value: copy)\n  emit Sum(total: items[:fold total be 0, entry => total + entry], picked: :List[:select n in 10 => n], ext: :navigation.intercept(n: unknownArgument))\n}\nfunction double(_ x) be x + x\nconstant $limit be 2","expect":[{"text":"double","kind":"function"}],"diagnostics":[{"code":"validate.unresolvedVariable","text":"unknownArgument"}]}
```

```highlight
{"name":"pattern values resolve like other expressions","language":"ges","source":"on Start(items, wanted) {\n  emit Has(found: items[:has [level: wanted]], gone: items[:has [level: lost]], pair: items[:take pair of none])\n}","expect":[{"text":"wanted","occurrence":1,"kind":"identifier"}],"diagnostics":[{"code":"validate.unresolvedVariable","text":"lost"},{"code":"validate.unresolvedVariable","text":"none"}]}
```

```highlight
{"name":"assembler names are not resolved","language":"gesa","source":".segment source \"bot.ges\"\non Start() { emit Done(value: missing) }\n.region-end \"source\"\n.segment code\nLoadInteger answer, 0\nAdd answer, answer, missing\n","expect":[{"text":"LoadInteger","kind":"keyword"}],"diagnostics":[]}
```

```highlight
{"name":"schema source categories","language":"gess","source":"// note\nconstant $max be 135\nparameter width: :Quantity(m)\nschema :Bot as {\n  id: :Number integer between 0 and $max key\n  x: :Quantity(m) between 0m and width step 1cm\n  kind: :Tag(#a) | :Text\n}\nmessage Hit(_ damage: :Boolean)\n","expect":[{"text":"// note","kind":"comment"},{"text":"constant","kind":"keyword"},{"text":"$max","occurrence":0,"kind":"constant"},{"text":"135","kind":"number"},{"text":":Quantity","occurrence":0,"kind":"type"},{"text":"schema","kind":"keyword"},{"text":":Bot","kind":"type"},{"text":"between","occurrence":0,"kind":"keyword"},{"text":"key","kind":"keyword"},{"text":"1cm","kind":"number"},{"text":"#a","kind":"tag"},{"text":"Hit","kind":"message"}],"diagnostics":[]}
```

```highlight
{"name":"schema roles","language":"gess","source":"wire(world) model(view) schema :Arena as { size: :Number integer between 1 and 64 }\nwire messages :Events as (Hit)\nmessage Hit()\n","expect":[{"text":"wire","occurrence":0,"kind":"keyword"},{"text":"model","kind":"keyword"},{"text":"schema","kind":"keyword"},{"text":"wire","occurrence":1,"kind":"keyword"},{"text":"messages","kind":"keyword"}],"diagnostics":[]}
```

```highlight
{"name":"schema role on a constant","language":"gess","source":"wire constant $a be 1\n","expect":[{"text":"wire","kind":"keyword"}],"diagnostics":[{"code":"schema.invalidRole","text":"w"}]}
```

```highlight
{"name":"schema source first syntax error","language":"gess","source":"schema :A as { a }\n","expect":[{"text":"schema","kind":"keyword"}],"diagnostics":[{"code":"schema.syntax","text":"}"}]}
```

```highlight
{"name":"inline assembly operands are not resolved","language":"ges","source":"on Start(input) {\n  asm {\n    .register temp, list\n    LoadInteger temp, input\n    Call temp, scale(value: temp, by: list[1])\n    CreateList list, [temp, temp]\n    emit Done(value: temp, first: list[1], total: list[:fold acc be 0, item => acc + temp])\n  }\n}","expect":[{"text":"asm","kind":"keyword"}],"diagnostics":[]}
```

```highlight
{"name":"unterminated string runs to the end","language":"ges","source":"on Start() { emit Done('open\nemit Next() }","expect":[{"text":"'open\nemit Next() }","kind":"string"}],"diagnostics":[{"code":"parse.syntax","text":"'open\nemit Next() }"},{"code":"parse.syntax","text":"}","after":true,"length":0}]}
```

```highlight
{"name":"declared names calls and records","language":"ges","source":"module game.rules\nrecord :Card as {\n  _ rank: :Number,\n  twice: :Number computed by rank * 2\n}\nfunction double(_ x) be x + x\non Start() { emit Done(value: double(2), card: :Card(1).twice, tag: #s) }","expect":[{"text":"game.rules","kind":"module"},{"text":":Card","kind":"type"},{"text":"_","kind":"symbol"},{"text":"rank","kind":"identifier"},{"text":"computed","kind":"keyword"},{"text":"double","kind":"function"},{"text":"Start","kind":"message"},{"text":"double","occurrence":1,"kind":"function"},{"text":":Card","occurrence":1,"kind":"type"},{"text":"twice","occurrence":1,"kind":"identifier"},{"text":"value","kind":"identifier"},{"text":"#s","kind":"tag"}]}
```

```highlight
{"name":"contextual keywords and intrinsics","language":"ges","source":"let a be values[:take first 2]\nlet b be values[:sort ascending]\nlet c be angle between(v, w)\nlet d be x is less than 3\nfor i from 1 to 9 step 2 {}","expect":[{"text":":take","kind":"builtin"},{"text":"first","kind":"keyword"},{"text":"ascending","kind":"keyword"},{"text":"angle","kind":"builtin"},{"text":"between","kind":"builtin"},{"text":"less","kind":"keyword"},{"text":"than","kind":"keyword"},{"text":"from","kind":"keyword"},{"text":"step","kind":"keyword"}]}
```

```highlight
{"name":"dice units and math constants","language":"ges","source":"let r be roll dice 2d6\nlet s be 2x + pi\nlet q be speed as :Quantity(m/s)","expect":[{"text":"roll","kind":"keyword"},{"text":"2d6","kind":"number"},{"text":"x","occurrence":0,"kind":"identifier"},{"text":"pi","kind":"number"},{"text":":Quantity","kind":"type"},{"text":"m/s","kind":"builtin"},{"text":"(","occurrence":0,"kind":"symbol"}]}
```

```highlight
{"name":"brackets braces and argument labels","language":"ges","source":"let m be [a: 1, b: 2]\non Start() { emit Done(to: 1, nothing: [1, 2], abs: m) }","expect":[{"text":"[","kind":"symbol"},{"text":"a","occurrence":0,"kind":"identifier"},{"text":"{","kind":"symbol"},{"text":"}","kind":"symbol"},{"text":"to","kind":"identifier"},{"text":"nothing","kind":"identifier"},{"text":"abs","kind":"identifier"}]}
```

```highlight
{"name":"extension names","language":"ges","source":"let d be :math.distance(x: 1, y: 2)","expect":[{"text":":math.distance","kind":"builtin"},{"text":"x","kind":"identifier"}]}
```

```highlight
{"name":"assembler labels registers and units","language":"gesa","source":".segment code\nStart:              // handler Start()\n    @0000  LoadInteger r5, #6, unit:newton\n           Multiply r1, r0(value), r0(value) mode=Numeric\n           Jump L_0\nL_0:       ReturnVoid\n","expect":[{"text":"code","kind":"tag"},{"text":"Start","kind":"label"},{"text":"// handler Start()","kind":"comment"},{"text":"@0000","kind":"comment"},{"text":"LoadInteger","kind":"keyword"},{"text":"r5","kind":"register"},{"text":"#6","kind":"number"},{"text":"unit","kind":"keyword"},{"text":":newton","kind":"tag"},{"text":"r0","kind":"register"},{"text":"value","kind":"label"},{"text":"mode","kind":"identifier"},{"text":"Numeric","kind":"type"},{"text":"L_0","kind":"label"},{"text":"L_0","occurrence":1,"kind":"label"}]}
```

```highlight
{"name":"assembler text with line breaks and regions","language":"gesa","source":".region \"Text\"\n.segment text\nT_note:             .text \"first\nNULL: second\"\nT_x:                .text \"x\"\n.region-end \"Text\"\n","expect":[{"text":".region \"Text\"","kind":"comment"},{"text":".text","kind":"keyword"},{"text":"\"first\nNULL: second\"","kind":"string"},{"text":"T_x","kind":"label"},{"text":".region-end \"Text\"","kind":"comment"}]}
```

```highlight
{"name": "document header keys and scalars", "language": "gesd", "source": ":Schema(:DriveUnit)\n// Engine and gearbox\nid: 'drive.tracked.basic'\nkind: #drive\ntier: 1\nmaxSpeed: 10m/s\nefficiency: 90%\nready: true\n", "expect": [{"text": ":Schema", "kind": "keyword"}, {"text": ":DriveUnit", "kind": "type"}, {"text": "// Engine and gearbox", "kind": "comment"}, {"text": "id", "kind": "identifier"}, {"text": ":", "kind": "symbol", "occurrence": 2}, {"text": "'drive.tracked.basic'", "kind": "string"}, {"text": "#drive", "kind": "tag"}, {"text": "1", "kind": "number"}, {"text": "10m/s", "kind": "number"}, {"text": "90%", "kind": "number"}, {"text": "true", "kind": "keyword"}], "diagnostics": []}
```

```highlight
{"name": "document series kinds and message forms", "language": "gesd", "source": "a: :Series(fibonacci)\nb: :Series(offset: 2, kind: factorial)\nc: :Message(Done())\n", "expect": [{"text": "fibonacci", "kind": "builtin"}, {"text": "offset", "kind": "identifier"}, {"text": "kind", "kind": "identifier"}, {"text": "factorial", "kind": "builtin"}, {"text": "Done", "kind": "message"}], "diagnostics": []}
```

```highlight
{"name": "document containers and forms", "language": "gesd", "source": "cost: [buildCost: 80, 'weight': 550kg]\nmount: :Vector(x: 0m, y: -0.8m)\nrolls: :Dice[6, 3]\nrange: :Range(from: 1, to: 5)\nseries: :Series(fibonacci)\n", "expect": [{"text": "[", "kind": "symbol"}, {"text": "buildCost", "kind": "identifier"}, {"text": "'weight'", "kind": "string"}, {"text": "550kg", "kind": "number"}, {"text": ":Vector", "kind": "type"}, {"text": "x", "kind": "identifier", "occurrence": 0}, {"text": "-0.8m", "kind": "number"}, {"text": ":Dice", "kind": "type"}, {"text": "6", "kind": "number"}, {"text": "from", "kind": "identifier"}, {"text": "fibonacci", "kind": "builtin"}], "diagnostics": []}
```

```highlight
{"name": "document messages and handlers", "language": "gesd", "source": "done: :Message(Done(v: 1) with #a)\non: :Handler(Done(v, _))\n", "expect": [{"text": ":Message", "kind": "type"}, {"text": "Done", "kind": "message"}, {"text": "v", "kind": "identifier"}, {"text": "with", "kind": "keyword"}, {"text": "#a", "kind": "tag"}, {"text": ":Handler", "kind": "type"}, {"text": "Done", "kind": "message", "occurrence": 1}, {"text": "v", "kind": "identifier", "occurrence": 1}, {"text": "_", "kind": "identifier"}], "diagnostics": []}
```

```highlight
{"name": "document unquoted text", "language": "gesd", "source": "name: Basic Track Drive\n", "expect": [{"text": "name", "kind": "identifier"}, {"text": "Basic", "kind": "plain"}], "diagnostics": [{"code": "document.unquotedText", "text": "Basic"}]}
```

```highlight
{"name": "document unknown unit and comment end", "language": "gesd", "source": "cooling: 2   // per second\nspeed: 12furlongs// note\n", "expect": [{"text": "// per second", "kind": "comment"}, {"text": "12furlongs", "kind": "number"}, {"text": "// note", "kind": "comment"}], "diagnostics": [{"code": "document.unknownUnit", "text": "12furlongs"}]}
```

```highlight
{"name": "document unclosed bracket and duplicate key", "language": "gesd", "source": "a: 1\na: [1,\n  2\n", "expect": [{"text": "a", "kind": "identifier", "occurrence": 1}], "diagnostics": [{"code": "document.duplicateKey", "text": "a", "occurrence": 1}]}
```

```highlight
{"name": "document unclosed list", "language": "gesd", "source": "items: [1, 2\n", "expect": [{"text": "[", "kind": "symbol"}], "diagnostics": [{"code": "document.unclosedBracket", "text": "["}]}
```

```highlight
{"name": "document missing colon at a line break", "language": "gesd", "source": "tier 1\n", "expect": [{"text": "tier", "kind": "plain"}], "diagnostics": [{"code": "document.missingColon", "text": "tier ", "after": true, "length": 1}]}
```

```highlight
{"name": "document unknown type and misplaced header", "language": "gesd", "source": "hit: :Hit(5)\n", "expect": [{"text": ":Hit", "kind": "type"}], "diagnostics": [{"code": "document.unknownType", "text": ":Hit"}]}
```

```highlight
{"name": "document NaN constructs no value", "language": "gesd", "source": "ratio: NaN\n", "expect": [{"text": "NaN", "kind": "number"}], "diagnostics": [{"code": "document.invalidLiteral", "text": "NaN"}]}
```

```highlight
{"name": "document unclosed text runs to the end", "language": "gesd", "source": "\ufeffname: 'open\nnext: 1\n", "expect": [{"text": "'open\nnext: 1\n", "kind": "string"}], "diagnostics": [{"code": "document.unclosedText", "text": "'"}]}
```

```highlight
{"name":"underscore names","language":"ges","source":"record :Unit_Weapon as {\n  a: :Number\n}\non Unit_Fire(x) {\n  emit Unit_Fire(x: x is :Unit_Weapon)\n}\n","expect":[{"text":":Unit_Weapon","occurrence":0,"kind":"type"},{"text":":Unit_Weapon","occurrence":1,"kind":"type"},{"text":"Unit_Fire","occurrence":0,"kind":"message"},{"text":"Unit_Fire","occurrence":1,"kind":"message"}]}
```

```highlight
{"name":"schema underscore names","language":"gess","source":"schema :Unit_Weapon as { a: :Boolean }\nmessage Unit_Fire()\n","expect":[{"text":":Unit_Weapon","kind":"type"},{"text":"Unit_Fire","kind":"message"}],"diagnostics":[]}
```

```highlight
{"name":"schema names with a doubled underscore","language":"gess","source":"schema :Unit__Weapon as { a: :Boolean }\n","expect":[{"text":":Unit","kind":"type"}],"diagnostics":[{"code":"schema.syntax","text":"_"}]}
```

```highlight
{"name":"document underscore names","language":"gesd","source":":Schema(:Unit_Weapon)\ndone: :Message(Unit_Fire(v: 1))\non: :Handler(Unit_Fire(v))\n","expect":[{"text":":Unit_Weapon","kind":"type"},{"text":"Unit_Fire","occurrence":0,"kind":"message"},{"text":"Unit_Fire","occurrence":1,"kind":"message"}],"diagnostics":[]}
```
