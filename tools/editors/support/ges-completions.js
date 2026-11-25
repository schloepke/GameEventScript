// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0
'use strict';
// Small syntax-only completion provider for VS Code, sharing completions.json with Sublime Text and TextMate.

const data = require('./completions.json');
const contexts = data.contexts.map(context => ({ ...context, regex: new RegExp(context.pattern) }));

// True when the end of the line text lies in a comment or an unfinished string literal.
function inCommentOrString(before) {
    let quote = null;
    for (let index = 0; index < before.length; index++) {
        const char = before[index];
        if (quote) {
            if (char === quote) quote = null;
        } else if (char === "'" || char === '"') {
            quote = char;
        } else if (char === '/' && before[index + 1] === '/') {
            return true;
        }
    }
    return quote !== null;
}

// The candidate words for the line text before the caret; null in comments and strings.
function candidates(before) {
    if (inCommentOrString(before)) return null;
    for (const context of contexts) {
        const match = context.regex.exec(before);
        if (match) {
            const prefix = context.typePrefix && !match[1] ? ':' : '';
            return data[context.group].map(word => prefix + word);
        }
    }
    return data.keywords;
}

function registerCompletions(vscode) {
    return vscode.languages.registerCompletionItemProvider('ges', {
        provideCompletionItems(document, position) {
            const before = document.lineAt(position.line).text.slice(0, position.character);
            const words = candidates(before);
            if (!words) return undefined;
            // Replace the typed word, including a leading colon that a candidate repeats.
            const typed = /:?[A-Za-z][A-Za-z0-9]*$|:$/.exec(before)?.[0] ?? '';
            return words.map(word => {
                const colon = word.startsWith(':') && typed.startsWith(':');
                const length = typed.startsWith(':') && !colon ? typed.length - 1 : typed.length;
                const bare = word.replace(/^:/, '');
                const kind = data.keywords.includes(word) ? vscode.CompletionItemKind.Keyword
                    : data.types.includes(bare) ? vscode.CompletionItemKind.Class : vscode.CompletionItemKind.Function;
                const item = new vscode.CompletionItem(word, kind);
                item.range = new vscode.Range(position.line, position.character - length, position.line, position.character);
                item.filterText = colon ? word : bare;
                item.detail = 'GES';
                return item;
            });
        },
    }, ':', '[');
}

module.exports = { candidates, registerCompletions };
