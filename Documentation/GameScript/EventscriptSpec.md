# EventScript - Formale Spezifikation

## Überblick

EventScript ist eine minimalistische, komplett event-basierte Script-Sprache zur Erweiterung und Anpassung von einfachen Spielen in Unity 6. Die Sprache erlaubt keine klassischen Funktionen oder globalen Zustände, sondern basiert ausschließlich auf der Reaktion auf Events und dem Auslösen neuer Events.

---

## Grundbaustein: Event-Handler

```eventscript
on <EventName>([param1, param2, ...]) {
    // Statements
}
```

- Events sind Einstiegspunkte.
- Parameter sind optional.
- Mehrere Handler für dasselbe Event sind erlaubt.

---

## Events senden

```eventscript
emit <EventName>([argument1, argument2, ...]);
```

- Events sind die einzige Form von Kommunikation und Effekt.
- Events können rekursiv und verschachtelt ausgelöst werden.

---

## 3. Variablen

```eventscript
let name = "Player";
let hp = 100;
```

- Nur "let" (immutable Binding)
- Gültig nur innerhalb des jeweiligen Blocks

---

## Kontrollstrukturen

### If / Else

```eventscript
if <Bedingung> {
    // true-Zweig
} else {
    // false-Zweig
}
```

### For-In

```eventscript
for <element> in <collection> {
    // Schleifenrumpf
}
```

- Iteriert über Listen oder virtuelle Ranges.
- Collection kann aus Event-Parametern stammen oder direkt erzeugt werden.

---

## Datentypen

### Primitive

- Number
- String
- Boolean

### Strukturierte Typen

- List<T>
- Object (zugreifbar mit Punktnotation)

---

## Operatoren

### Arithmetisch

- `+`, `-`, `*`, `/`, `%`

### Vergleich

- `==`, `!=`, `<`, `>`, `<=`, `>=`

### Logisch

- `&&`, `||`, `!`

---

## Built-in Funktionen

| Funktion              | Beschreibung                                   |
|-----------------------|------------------------------------------------|
| `range(a, b)`         | Erzeugt Liste von Zahlen a bis b-1            |
| `length(list)`        | Gibt die Länge einer Liste zurück            |
| `contains(list, x)`   | true, wenn x in list                          |
| `random(min, max)`    | Gibt eine zufällige Zahl zwischen min und max |

---

## Kommentare

```eventscript
// Dies ist ein Kommentar
```

---

## Struktur & Modularität

- Keine Funktionen oder Klassen
- Wiederverwendbare Logik erfolgt über Events
- Modularität durch lose gekoppelte Event-Handler

---

## Bewusst nicht enthalten

- Kein globaler Zustand
- Keine Funktionsdefinitionen
- Keine Objektorientierung
- Keine Zählerschleifen (nur Range + for-in)

---

## Beispiel: Collectible-System

```eventscript
on Start() {
    emit InitPlayer("Player1");
    emit SpawnCoins(range(0, 10));
}

on CoinCollected(coin, player) {
    emit AddScore(player, coin.value);

    if coin.value > 10 {
        emit ShowMessage("Big coin!");
    }
}
```

---

## Parser-Grammatik (BNF)

```bnf
<program> ::= { <event_handler> }

<event_handler> ::= "on" <identifier> "(" [ <param_list> ] ")" "{" { <statement> } "}"
<param_list> ::= <identifier> { "," <identifier> }

<statement> ::= <emit_statement>
             | <let_statement>
             | <if_statement>
             | <for_statement>
             | <expression_statement>

<emit_statement> ::= "emit" <identifier> "(" [ <arg_list> ] ")" ";"
<let_statement> ::= "let" <identifier> "=" <expression> ";"
<if_statement> ::= "if" <expression> "{" { <statement> } "}" [ "else" "{" { <statement> } "}" ]
<for_statement> ::= "for" <identifier> "in" <expression> "{" { <statement> } "}"
<expression_statement> ::= <expression> ";"

<arg_list> ::= <expression> { "," <expression> }

<expression> ::= <literal>
              | <identifier>
              | <expression> <binary_op> <expression>
              | <unary_op> <expression>
              | <expression> "." <identifier>
              | <identifier> "(" [ <arg_list> ] ")"

<binary_op> ::= "+" | "-" | "*" | "/" | "%"
              | "==" | "!=" | "<" | ">" | "<=" | ">="
              | "&&" | "||"

<unary_op> ::= "!"

<literal> ::= <number> | <string> | "true" | "false"

<identifier> ::= [a-zA-Z_][a-zA-Z0-9_]*
<number> ::= [0-9]+ ("." [0-9]+)?
<string> ::= '"' { <any_char_except_"> } '"'
```

---

## Weiteres Vorgehen
- [ ] Interpreter/Compiler in C#
- [ ] Editorintegration in Unity
- [ ] Validierung & statische Analyse
- [ ] Syntax Highlighter für Rider bzw. Inside Unity Editor
- [ ] State Machine + handling integrieren
- [ ] Interaktion mit einem Spiel Objekt (2D Tile based + Sprites)
- [ ] Semikolon als statement end marker entfernen (falls möglich)
- [ ] Emit conditions in etwa wie `emit PlayerHealth(20) onlyif regenerateHitAbility == true`
