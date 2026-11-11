// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import SwiftSyntax
import SwiftSyntaxBuilder
import SwiftSyntaxMacros

private struct BindingError: Error, CustomStringConvertible {
    let description: String

    init(_ description: String) { self.description = description }
}

private func attribute(_ name: String, in attributes: AttributeListSyntax) -> AttributeSyntax? {
    attributes.compactMap { $0.as(AttributeSyntax.self) }.first { $0.attributeName.trimmedDescription.split(separator: ".").last == Substring(name) }
}

private func literal(_ expression: ExprSyntax) throws -> String {
    guard let value = expression.as(StringLiteralExprSyntax.self), value.segments.count == 1,
        let segment = value.segments.first?.as(StringSegmentSyntax.self), !segment.content.text.contains("\\")
    else { throw BindingError("GES annotation names must be plain string literals without interpolation or escapes.") }
    return segment.content.text
}

private func arguments(_ node: AttributeSyntax) -> [String: ExprSyntax] {
    guard case .argumentList(let list) = node.arguments else { return [:] }
    return Dictionary(list.map { ($0.label?.text ?? "name", $0.expression) }, uniquingKeysWith: { first, _ in first })
}

private func stringArgument(_ key: String, in args: [String: ExprSyntax]) throws -> String? {
    guard let value = args[key], !value.is(NilLiteralExprSyntax.self) else { return nil }
    return try literal(value)
}

private func quoted(_ text: String) -> String { StringLiteralExprSyntax(content: text).description }

private func inferredType(_ type: TypeSyntax) -> String? {
    if let optional = type.as(OptionalTypeSyntax.self) { return inferredType(optional.wrappedType) }
    if let optional = type.as(ImplicitlyUnwrappedOptionalTypeSyntax.self) { return inferredType(optional.wrappedType) }
    if let array = type.as(ArrayTypeSyntax.self) {
        let element = array.element.trimmedDescription
        return element == "UInt8" || element == "Swift.UInt8" ? "Bytes" : "List"
    }
    if type.is(DictionaryTypeSyntax.self) { return "Map" }
    let name: String
    let generics: GenericArgumentClauseSyntax?
    if let identifier = type.as(IdentifierTypeSyntax.self) {
        name = identifier.name.text
        generics = identifier.genericArgumentClause
    } else if let member = type.as(MemberTypeSyntax.self), member.baseType.trimmedDescription == "Swift" {
        name = member.name.text
        generics = member.genericArgumentClause
    } else {
        return nil
    }
    switch name {
    case "Bool": return "Boolean"
    case "String": return "Text"
    case "Double", "Float", "Int", "Int8", "Int16", "Int32", "Int64", "UInt", "UInt8", "UInt16", "UInt32", "UInt64": return "Number"
    case "Array": return generics?.arguments.first.map { ["UInt8", "Swift.UInt8"].contains($0.argument.trimmedDescription) } == true ? "Bytes" : "List"
    case "Dictionary": return "Map"
    case "Optional": return generics?.arguments.first.flatMap { inferredType($0.argument) }
    default: return nil
    }
}

private struct Field {
    let swiftName: String
    let name: String
    let typeName: String
    let unit: String?
}

private final class ConditionalBindingVisitor: SyntaxVisitor {
    var found = false

    override func visit(_ node: AttributeSyntax) -> SyntaxVisitorContinueKind {
        if ["GesField", "GesConstruct"].contains(String(node.attributeName.trimmedDescription.split(separator: ".").last ?? "")) { found = true }
        return .skipChildren
    }
}

struct GesTypeMacro: MemberMacro {
    // GesQuantityUnit cases other than none with their canonical symbols; the macro target cannot import the runtime,
    // so this table mirrors the enum.
    static let unitSymbols: [String: String] = [
        "degree": "°", "meter": "m", "second": "s", "meterPerSecond": "m/s", "meterPerSecondSquared": "m/s²", "kilogram": "kg", "newton": "N",
        "joule": "J", "watt": "W", "volt": "V", "ampere": "A", "hertz": "Hz", "squareMeter": "m²", "cubicMeter": "m³", "wattPerSquareMeter": "W/m²",
        "kelvin": "K", "degreePerSecond": "°/s", "decibel": "dB", "wattPerKelvin": "W/K", "joulePerKelvin": "J/K",
    ]

    static func expansion(of node: AttributeSyntax, providingMembersOf declaration: some DeclGroupSyntax, in context: some MacroExpansionContext) throws -> [DeclSyntax] {
        let typeName: String
        if let type = declaration.as(StructDeclSyntax.self), type.genericParameterClause == nil {
            typeName = type.name.text
        } else if let type = declaration.as(ClassDeclSyntax.self), type.genericParameterClause == nil, type.modifiers.contains(where: { $0.name.text == "final" }) {
            typeName = type.name.text
        } else {
            throw BindingError("@GesType requires a nongeneric struct or final class.")
        }
        guard !declaration.attributes.contains(where: { $0.as(AttributeSyntax.self)?.attributeName.trimmedDescription == "MainActor" })
        else { throw BindingError("@GesType bindings must be synchronous and nonisolated.") }
        let name = try stringArgument("name", in: arguments(node)) ?? typeName
        var fields: [Field] = []
        for member in declaration.memberBlock.members {
            if let conditional = member.decl.as(IfConfigDeclSyntax.self) {
                let visitor = ConditionalBindingVisitor(viewMode: .sourceAccurate)
                visitor.walk(conditional)
                if visitor.found { throw BindingError("Conditional @GesField/@GesConstruct declarations require manual bindings.") }
            }
            if let function = member.decl.as(FunctionDeclSyntax.self), function.name.text == "createGesType" {
                throw BindingError("@GesType reserves the member name createGesType.")
            }
            guard let property = member.decl.as(VariableDeclSyntax.self), let annotation = attribute("GesField", in: property.attributes) else { continue }
            guard property.bindings.count == 1, let binding = property.bindings.first,
                let identifier = binding.pattern.as(IdentifierPatternSyntax.self), let type = binding.typeAnnotation?.type,
                !property.modifiers.contains(where: { ["static", "class"].contains($0.name.text) })
            else { throw BindingError("@GesField requires one instance property with an explicit Swift type.") }
            if let accessors = binding.accessorBlock, case .accessors(let list) = accessors.accessors,
                list.contains(where: { $0.effectSpecifiers != nil })
            {
                throw BindingError("@GesField requires a synchronous, nonthrowing getter; use an explicit getter binding otherwise.")
            }
            let args = arguments(annotation)
            let fieldName = try stringArgument("name", in: args) ?? identifier.identifier.text
            let explicitType = try stringArgument("typeName", in: args)
            var unit: String?
            if let expression = args["unit"] {
                guard let member = expression.as(MemberAccessExprSyntax.self),
                    member.base == nil || member.base?.trimmedDescription == "GesQuantityUnit" || member.base?.trimmedDescription == "GameEventScriptRuntime.GesQuantityUnit",
                    member.declName.baseName.text == "none" || Self.unitSymbols[member.declName.baseName.text] != nil
                else { throw BindingError("@GesField unit must be a literal GesQuantityUnit case.") }
                if member.declName.baseName.text != "none" { unit = member.declName.baseName.text }
            }
            guard unit == nil || explicitType == nil else { throw BindingError("Use either typeName or unit on @GesField, not both.") }
            guard let inferred = explicitType ?? inferredType(type) else {
                throw BindingError("Cannot infer the GES field type; specify @GesField(typeName: ...).")
            }
            guard unit == nil || inferred == "Number" else { throw BindingError("@GesField unit requires a native numeric property.") }
            let fieldType = unit.flatMap { Self.unitSymbols[$0] }.map { "Quantity(\($0))" } ?? inferred
            guard !fields.contains(where: { $0.name == fieldName }) else { throw BindingError("Duplicate @GesField name: \(fieldName)") }
            fields.append(Field(swiftName: identifier.identifier.text, name: fieldName, typeName: fieldType, unit: unit))
        }
        var constructors: [String] = []
        for member in declaration.memberBlock.members {
            let parameters: FunctionParameterListSyntax
            let call: String
            let throwsCall: Bool
            if let initializer = member.decl.as(InitializerDeclSyntax.self), attribute("GesConstruct", in: initializer.attributes) != nil {
                guard initializer.optionalMark == nil, initializer.genericParameterClause == nil, initializer.signature.effectSpecifiers?.asyncSpecifier == nil else {
                    throw BindingError("@GesConstruct requires a synchronous, nonfailable, nongeneric initializer.")
                }
                parameters = initializer.signature.parameterClause.parameters
                throwsCall = initializer.signature.effectSpecifiers?.throwsClause != nil
                call = typeName
            } else if let function = member.decl.as(FunctionDeclSyntax.self), attribute("GesConstruct", in: function.attributes) != nil {
                guard function.modifiers.contains(where: { $0.name.text == "static" }), function.genericParameterClause == nil,
                    function.signature.effectSpecifiers?.asyncSpecifier == nil,
                    ["Self", typeName].contains(function.signature.returnClause?.type.trimmedDescription ?? "")
                else { throw BindingError("@GesConstruct factories must be synchronous, nongeneric static functions returning Self or the enclosing type.") }
                parameters = function.signature.parameterClause.parameters
                throwsCall = function.signature.effectSpecifiers?.throwsClause != nil
                call = "\(typeName).`\(function.name.text)`"
            } else {
                continue
            }
            var labels: [String] = []
            var values: [String] = []
            for (index, parameter) in parameters.enumerated() {
                guard parameter.ellipsis == nil, !parameter.type.trimmedDescription.hasPrefix("inout "), parameter.modifiers.isEmpty else {
                    throw BindingError("@GesConstruct does not support variadic, inout or ownership-qualified parameters.")
                }
                let external = parameter.firstName.text
                let local = parameter.secondName?.text ?? external
                guard let field = fields.first(where: { $0.name == external }) ?? fields.first(where: { $0.swiftName == local }) else {
                    throw BindingError("@GesConstruct parameter \(local) must refer to an annotated field.")
                }
                labels.append(quoted(field.name))
                let decode = "try GesBridgeValueConversion.decode(arguments[\(index)], as: (\(parameter.type.trimmedDescription)).self\(field.unit.map { ", unit: .\($0)" } ?? ""))"
                values.append((external == "_" ? "" : "\(external): ") + decode)
            }
            constructors.append(".init(parameters: [\(labels.joined(separator: ", "))]) { arguments in \(throwsCall ? "try " : "")\(call)(\(values.joined(separator: ", "))) }")
        }
        let entries = fields.map { field in
            if let unit = field.unit {
                return ".init(\(quoted(field.name)), kind: .float, unit: .\(unit), get: { try GesBridgeValueConversion.encode($0.`\(field.swiftName)`, unit: .\(unit)) })"
            }
            return ".init(\(quoted(field.name)), typeName: \(quoted(field.typeName)), keyPath: \\\(typeName).`\(field.swiftName)` )"
        }
        let access = declaration.modifiers.contains(where: { $0.name.text == "public" }) ? "public " : declaration.modifiers.contains(where: { $0.name.text == "package" }) ? "package " : ""
        return [
            DeclSyntax(
                stringLiteral: """
                    /// Creates a validated native binding. Retain this descriptor for registration, wrapping and unwrapping.
                    /// Each invocation returns a distinct descriptor and may throw for invalid portable declarations.
                    \(access)static func createGesType() throws -> GesBridgeTypeBinding<\(typeName)> {
                        try GesBridgeTypeBinding(\(quoted(name)), fields: [\(entries.joined(separator: ", "))], constructors: [\(constructors.joined(separator: ", "))])
                    }
                    """
            )
        ]
    }
}

private func requireEnclosingType(_ context: some MacroExpansionContext) throws {
    for syntax in context.lexicalContext {
        if let type = syntax.as(StructDeclSyntax.self) {
            if attribute("GesType", in: type.attributes) != nil { return }
            break
        }
        if let type = syntax.as(ClassDeclSyntax.self) {
            if attribute("GesType", in: type.attributes) != nil { return }
            break
        }
        if syntax.is(ExtensionDeclSyntax.self) || syntax.is(EnumDeclSyntax.self) || syntax.is(ActorDeclSyntax.self) || syntax.is(FunctionDeclSyntax.self) {
            break
        }
    }
    throw BindingError("This annotation requires an enclosing @GesType declaration.")
}

struct GesFieldMacro: PeerMacro {
    static func expansion(of node: AttributeSyntax, providingPeersOf declaration: some DeclSyntaxProtocol, in context: some MacroExpansionContext) throws -> [DeclSyntax] {
        guard declaration.is(VariableDeclSyntax.self) else { throw BindingError("@GesField applies only to properties.") }
        try requireEnclosingType(context)
        return []
    }
}

struct GesConstructMacro: PeerMacro {
    static func expansion(of node: AttributeSyntax, providingPeersOf declaration: some DeclSyntaxProtocol, in context: some MacroExpansionContext) throws -> [DeclSyntax] {
        guard declaration.is(InitializerDeclSyntax.self) || declaration.is(FunctionDeclSyntax.self) else { throw BindingError("@GesConstruct applies only to initializers or static factories.") }
        try requireEnclosingType(context)
        return []
    }
}

// A literal GesQuantityUnit case such as `.meter`, or nil for `.none`.
private func unitCase(_ expression: ExprSyntax) throws -> String? {
    guard let member = expression.as(MemberAccessExprSyntax.self),
        member.base == nil || member.base?.trimmedDescription == "GesQuantityUnit" || member.base?.trimmedDescription == "GameEventScriptRuntime.GesQuantityUnit",
        member.declName.baseName.text == "none" || GesTypeMacro.unitSymbols[member.declName.baseName.text] != nil
    else { throw BindingError("GES units must be literal GesQuantityUnit cases.") }
    return member.declName.baseName.text == "none" ? nil : member.declName.baseName.text
}

private func units(_ expression: ExprSyntax?) throws -> [String: String] {
    guard let expression else { return [:] }
    guard let dictionary = expression.as(DictionaryExprSyntax.self) else { throw BindingError("units must be a dictionary literal of parameter names and units.") }
    guard case .elements(let elements) = dictionary.content else { return [:] }
    var result: [String: String] = [:]
    for element in elements {
        let name = try literal(element.key)
        guard result[name] == nil else { throw BindingError("units names the parameter \(name) twice.") }
        if let unit = try unitCase(element.value) { result[name] = unit }
    }
    return result
}

struct GesExtensionMacro: MemberMacro {
    static func expansion(of node: AttributeSyntax, providingMembersOf declaration: some DeclGroupSyntax, in context: some MacroExpansionContext) throws -> [DeclSyntax] {
        guard
            declaration.is(EnumDeclSyntax.self) || declaration.is(StructDeclSyntax.self)
                || declaration.as(ClassDeclSyntax.self)?.modifiers.contains(where: { $0.name.text == "final" }) == true
        else { throw BindingError("@GesExtension requires an enum, struct or final class.") }
        guard let namespace = try stringArgument("name", in: arguments(node)) else { throw BindingError("@GesExtension requires a namespace.") }
        var statics: [String] = []
        var instances: [String] = []
        var signatures: Set<String> = []
        for member in declaration.memberBlock.members {
            guard let function = member.decl.as(FunctionDeclSyntax.self) else { continue }
            if function.name.text == "gesExtensions" { throw BindingError("@GesExtension reserves the member name gesExtensions.") }
            let predicate = attribute("GesPredicate", in: function.attributes)
            guard let annotation = attribute("GesFunction", in: function.attributes) ?? predicate else { continue }
            guard function.genericParameterClause == nil, function.signature.effectSpecifiers?.asyncSpecifier == nil else {
                throw BindingError("@GesFunction requires a synchronous, nongeneric method.")
            }
            let args = arguments(annotation)
            let name = try stringArgument("name", in: args) ?? function.name.text
            let resultUnit = try args["unit"].flatMap(unitCase)
            let parameterUnits = try units(args["units"])
            let isStatic = function.modifiers.contains { ["static", "class"].contains($0.name.text) }
            guard let result = function.signature.returnClause?.type.trimmedDescription, result != "Void", result != "()" else {
                throw BindingError("@GesFunction requires a result.")
            }
            let parameters = Array(function.signature.parameterClause.parameters)
            if predicate != nil {
                guard parameters.count == 1, ["Bool", "Bool?", "Optional<Bool>"].contains(result) else { throw BindingError("@GesPredicate requires one parameter and a Bool or Bool? result.") }
            }
            var labels: [String] = []
            var conditions = ["call.arguments.count == \(parameters.count)"]
            var values: [String] = []
            var used: Set<String> = []
            for (index, parameter) in parameters.enumerated() {
                guard parameter.ellipsis == nil, parameter.defaultValue == nil, !parameter.type.trimmedDescription.hasPrefix("inout "), parameter.modifiers.isEmpty else {
                    throw BindingError("@GesFunction does not support variadic, defaulted, inout or ownership-qualified parameters.")
                }
                let external = parameter.firstName.text
                let local = parameter.secondName?.text ?? external
                let type = parameter.type.trimmedDescription
                let unit = parameterUnits[local]
                used.insert(local)
                if unit != nil && type == "GesValue" { throw BindingError("A GesValue parameter receives the raw argument and takes no unit.") }
                labels.append(predicate != nil ? "_" : external)
                conditions.append("let a\(index) = try? GesBridgeValueConversion.decode(call.arguments[\(index)], as: (\(type)).self, unit: .\(unit ?? "none"))")
                values.append((external == "_" ? "" : "\(external): ") + "a\(index)")
            }
            if let unknown = Set(parameterUnits.keys).subtracting(used).sorted().first { throw BindingError("units names \(unknown), which is no parameter.") }
            let signature = name + "(" + labels.joined(separator: ",") + ")"
            guard signatures.insert(signature).inserted else { throw BindingError("@GesExtension declares :\(namespace).\(signature) twice.") }
            let throwing = function.signature.effectSpecifiers?.throwsClause != nil
            let target = (isStatic ? "Self." : "self.") + "`\(function.name.text)`"
            let entry = """
                try GesBridgeExtension(namespace: \(quoted(namespace)), name: \(quoted(name)), parameters: [\(labels.map(quoted).joined(separator: ", "))]) { call in
                    guard \(conditions.joined(separator: ", ")) else {
                        call.setNothing()
                        return
                    }
                    let result = \(throwing ? "try " : "")\(target)(\(values.joined(separator: ", ")))
                    call.setValue((try? GesBridgeValueConversion.encode(result, unit: .\(resultUnit ?? "none"))) ?? .nothing)
                }
                """
            if isStatic { statics.append(entry) } else { instances.append(entry) }
        }
        let access = declaration.modifiers.contains(where: { $0.name.text == "public" }) ? "public " : declaration.modifiers.contains(where: { $0.name.text == "package" }) ? "package " : ""
        var members: [DeclSyntax] = []
        if !statics.isEmpty || instances.isEmpty {
            members.append(
                DeclSyntax(
                    stringLiteral: """
                        /// The extensions of the static `@GesFunction` and `@GesPredicate` methods, for a `GesBridgeExtensionRegistry`.
                        \(access)static func gesExtensions() throws -> [GesBridgeExtension] {
                            [\(statics.joined(separator: ",\n"))]
                        }
                        """
                )
            )
        }
        if !instances.isEmpty {
            members.append(
                DeclSyntax(
                    stringLiteral: """
                        /// The extensions of this instance's `@GesFunction` and `@GesPredicate` methods and the static ones; the closures
                        /// retain this instance for as long as a registry holds them.
                        \(access)func gesExtensions() throws -> [GesBridgeExtension] {
                            [\((instances + statics).joined(separator: ",\n"))]
                        }
                        """
                )
            )
        }
        return members
    }
}

struct GesFunctionMacro: PeerMacro {
    static func expansion(of node: AttributeSyntax, providingPeersOf declaration: some DeclSyntaxProtocol, in context: some MacroExpansionContext) throws -> [DeclSyntax] {
        guard declaration.is(FunctionDeclSyntax.self) else { throw BindingError("@GesFunction and @GesPredicate apply only to methods.") }
        for syntax in context.lexicalContext {
            if let type = syntax.asProtocol(WithAttributesSyntax.self), syntax.is(EnumDeclSyntax.self) || syntax.is(StructDeclSyntax.self) || syntax.is(ClassDeclSyntax.self) {
                if attribute("GesExtension", in: type.attributes) != nil { return [] }
                break
            }
        }
        throw BindingError("This annotation requires an enclosing @GesExtension declaration.")
    }
}
