// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

enum GesOperand: Int, Sendable {
    case targetRegister
    case outboundMessage
    case sourceRegister
    case leftRegister
    case rightRegister
    case operandRegister
    case returnRegister
    case messageRegister
    case handlerRegister
    case propertyRegister
    case objectRegister
    case builderRegister
    case itemRegister
    case keyRegister
    case valueRegister
    case itemBindingRegister
    case auxItemBindingRegister
    case weightRegister
    case conditionRegister
    case collectionRegister
    case iteratorRegister
    case sourceIteratorRegister
    case seriesRegister
    case indexRegister
    case defaultRegister
    case seedRegister
    case needleRegister
    case fromRegister
    case toRegister
    case stepRegister
    case minimumRegister
    case maximumRegister
    case auxARegister
    case auxBRegister
    case auxCRegister
    case auxDRegister
    case localRegisterDelta
    case diceCount
    case diceSideCount
    case componentCount
    case countImmediate
    case indexImmediate
    case fromImmediate
    case toImmediate
    case stepImmediate
    case integerImmediate
    case floatImmediate
    case unit
    case jumpTarget
    case entryTarget
    case callableEntry
    case predicateEntry
    case nextEntry
    case projectionEntry
    case keyEntry
    case valueEntry
    case faceRegister
    case string
    case text
    case tag
    case memberName
    case typeKind
    case patternKind
    case seriesKind
    case customTypeName
    case typeName
    case messageShapeList
    case argumentNameList
    case argumentRegisterList
    case itemRegisterList
    case keyNameList
    case valueRegisterList
    case captureRegisterList
    case tagRegisterList
    case recordReference
    case externalReference
}

extension GameEventScriptBytecodeOpCode {
    var isResultSend: Bool { rawValue >= 0xDA && rawValue <= 0xDD }

    var operands: [GesOperand] {
        switch self {
        case .hasPattern, .takePattern: [.targetRegister, .iteratorRegister, .patternKind]
        case .emitInstant, .emitAfter, .publishInstant, .publishAfter: [.targetRegister]
        case .`nop`: []
        case .`registerLocals`: [.localRegisterDelta]
        case .`jump`: [.jumpTarget]
        case .`jumpIfTrue`: [.conditionRegister, .jumpTarget]
        case .`jumpIfFalse`: [.conditionRegister, .jumpTarget]
        case .`jumpIfNotTrue`: [.conditionRegister, .jumpTarget]
        case .`jumpIfNothing`: [.conditionRegister, .jumpTarget]
        case .`call`: [.targetRegister, .callableEntry]
        case .`createSeries`: [.targetRegister, .seriesKind]
        case .`callExternal`: [.targetRegister, .externalReference, .argumentRegisterList]
        case .`returnVoid`: []
        case .`returnValue`: [.returnRegister]
        case .`emitMessage`: [.outboundMessage, .argumentRegisterList]
        case .`emitMessageWithTags`: [.outboundMessage, .argumentRegisterList, .tagRegisterList]
        case .`emitMessageValue`: [.messageRegister]
        case .`emitMessageValueWithTags`: [.messageRegister, .tagRegisterList]
        case .`publishMessage`: [.outboundMessage, .argumentRegisterList]
        case .`publishMessageWithTags`: [.outboundMessage, .argumentRegisterList, .tagRegisterList]
        case .`publishMessageValue`: [.messageRegister]
        case .`publishMessageValueWithTags`: [.messageRegister, .tagRegisterList]
        case .`cast`: [.targetRegister, .sourceRegister, .typeKind]
        case .`castCustom`: [.targetRegister, .sourceRegister, .customTypeName]
        case .`castUnit`: [.targetRegister, .sourceRegister, .unit]
        case .`castNumeric`: [.targetRegister, .sourceRegister]
        case .`parseLiteral`: [.targetRegister, .sourceRegister]
        case .`checkType`: [.targetRegister, .sourceRegister, .typeKind]
        case .`checkCustomType`: [.targetRegister, .sourceRegister, .customTypeName]
        case .`checkUnit`: [.targetRegister, .sourceRegister, .unit]
        case .`checkNumeric`: [.targetRegister, .sourceRegister]
        case .`checkInteger`: [.targetRegister, .sourceRegister]
        case .`checkFractional`: [.targetRegister, .sourceRegister]
        case .`move`: [.targetRegister, .sourceRegister]
        case .`memberAccess`: [.targetRegister, .memberName, .objectRegister]
        case .`indexAccess`: [.targetRegister, .indexImmediate, .objectRegister]
        case .`propertyAccess`: [.targetRegister, .propertyRegister, .objectRegister]
        case .`bindHandler`: [.targetRegister, .handlerRegister, .argumentRegisterList]
        case .`loadNothing`: [.targetRegister]
        case .`loadTrue`: [.targetRegister]
        case .`loadFalse`: [.targetRegister]
        case .`loadInteger`: [.targetRegister, .integerImmediate, .unit]
        case .`loadFloat`: [.targetRegister, .floatImmediate, .unit]
        case .`loadPercentage`: [.targetRegister, .floatImmediate]
        case .`loadText`: [.targetRegister, .text]
        case .`loadTag`: [.targetRegister, .tag]
        case .`loadHandler`: [.targetRegister, .messageShapeList]
        case .`loadMessage`: [.targetRegister, .messageShapeList, .argumentRegisterList]
        case .`stageRegister`: [.sourceRegister]
        case .`stageNothing`: []
        case .`stageTrue`: []
        case .`stageFalse`: []
        case .`stageInteger`: [.integerImmediate, .unit]
        case .`stageFloat`: [.floatImmediate, .unit]
        case .`stageText`: [.text]
        case .`stageTag`: [.tag]
        case .`stagePercentage`: [.floatImmediate]
        case .`createDice`: [.targetRegister, .diceCount, .diceSideCount]
        case .`createVector`: [.targetRegister, .componentCount]
        case .`createPoint`: [.targetRegister, .componentCount]
        case .`createList`: [.targetRegister]
        case .`createMap`: [.targetRegister, .keyNameList]
        case .`createRange`: [.targetRegister, .fromRegister, .toRegister]
        case .`createRangeWithStep`: [.targetRegister, .fromRegister, .toRegister, .stepRegister]
        case .`createRangeIterator`: [.targetRegister, .fromRegister, .toRegister]
        case .`createRangeIteratorWithStep`: [.targetRegister, .fromRegister, .toRegister, .stepRegister]
        case .`createRangeIteratorShort`: [.targetRegister, .fromImmediate, .toImmediate, .stepImmediate]
        case .`createRecord`: [.targetRegister, .recordReference]
        case .`createRecordValue`: [.targetRegister, .sourceRegister, .customTypeName]
        case .`createExternalType`: [.targetRegister, .externalReference, .argumentNameList]
        case .`hasValue`: [.targetRegister, .operandRegister]
        case .`isEmpty`: [.targetRegister, .operandRegister]
        case .`default`: [.targetRegister, .leftRegister, .rightRegister]
        case .`or`: [.targetRegister, .leftRegister, .rightRegister]
        case .`and`: [.targetRegister, .leftRegister, .rightRegister]
        case .`xor`: [.targetRegister, .leftRegister, .rightRegister]
        case .`implies`: [.targetRegister, .leftRegister, .rightRegister]
        case .`not`: [.targetRegister, .operandRegister]
        case .`equal`: [.targetRegister, .leftRegister, .rightRegister]
        case .`notEqual`: [.targetRegister, .leftRegister, .rightRegister]
        case .`less`: [.targetRegister, .leftRegister, .rightRegister]
        case .`greater`: [.targetRegister, .leftRegister, .rightRegister]
        case .`lessOrEqual`: [.targetRegister, .leftRegister, .rightRegister]
        case .`greaterOrEqual`: [.targetRegister, .leftRegister, .rightRegister]
        case .`add`: [.targetRegister, .leftRegister, .rightRegister]
        case .`subtract`: [.targetRegister, .leftRegister, .rightRegister]
        case .`multiply`: [.targetRegister, .leftRegister, .rightRegister]
        case .`divide`: [.targetRegister, .leftRegister, .rightRegister]
        case .`power`: [.targetRegister, .leftRegister, .rightRegister]
        case .`integerDivide`: [.targetRegister, .leftRegister, .rightRegister]
        case .`modulo`: [.targetRegister, .leftRegister, .rightRegister]
        case .`remainder`: [.targetRegister, .leftRegister, .rightRegister]
        case .`min`: [.targetRegister, .leftRegister, .rightRegister]
        case .`max`: [.targetRegister, .leftRegister, .rightRegister]
        case .`negate`: [.targetRegister, .operandRegister]
        case .`abs`: [.targetRegister, .operandRegister]
        case .`logN`: [.targetRegister, .operandRegister]
        case .`chance`: [.targetRegister, .operandRegister]
        case .`clamp`: [.targetRegister, .sourceRegister, .minimumRegister, .maximumRegister]
        case .`randomTake`: [.targetRegister, .fromRegister, .toRegister]
        case .`randomTakeFloat`: [.targetRegister, .fromRegister, .toRegister]
        case .`randomPush`: [.seedRegister]
        case .`randomPushConstant`: [.integerImmediate]
        case .`randomPop`: []
        case .`term`: [.targetRegister, .seriesRegister, .indexRegister]
        case .`exp`: [.targetRegister, .operandRegister]
        case .`floor`: [.targetRegister, .operandRegister]
        case .`ceil`: [.targetRegister, .operandRegister]
        case .`truncate`: [.targetRegister, .operandRegister]
        case .`roundHalfEven`: [.targetRegister, .operandRegister]
        case .`roundHalfUp`: [.targetRegister, .operandRegister]
        case .`roundHalfDown`: [.targetRegister, .operandRegister]
        case .`degreeToRadians`: [.targetRegister, .operandRegister]
        case .`degreeFromRadians`: [.targetRegister, .operandRegister]
        case .`wrapDegree`: [.targetRegister, .operandRegister]
        case .`sin`: [.targetRegister, .operandRegister]
        case .`cos`: [.targetRegister, .operandRegister]
        case .`tan`: [.targetRegister, .operandRegister]
        case .`asin`: [.targetRegister, .operandRegister]
        case .`acos`: [.targetRegister, .operandRegister]
        case .`atan`: [.targetRegister, .operandRegister]
        case .`atan2`: [.targetRegister, .leftRegister, .rightRegister]
        case .`hypot2D`: [.targetRegister, .leftRegister, .rightRegister]
        case .`hypot3D`: [.targetRegister, .leftRegister, .rightRegister, .auxARegister]
        case .`distance`: [.targetRegister, .leftRegister, .rightRegister]
        case .`distance2D`: [.targetRegister, .leftRegister, .rightRegister, .auxARegister, .auxBRegister]
        case .`distance3D`: [.targetRegister, .leftRegister, .rightRegister, .auxARegister, .auxBRegister, .auxCRegister, .auxDRegister]
        case .`distanceSquared`: [.targetRegister, .leftRegister, .rightRegister]
        case .`distanceSquared2D`: [.targetRegister, .leftRegister, .rightRegister, .auxARegister, .auxBRegister]
        case .`distanceSquared3D`: [.targetRegister, .leftRegister, .rightRegister, .auxARegister, .auxBRegister, .auxCRegister, .auxDRegister]
        case .`lengthSquared`: [.targetRegister, .operandRegister]
        case .`lengthSquared2D`: [.targetRegister, .leftRegister, .rightRegister]
        case .`lengthSquared3D`: [.targetRegister, .leftRegister, .rightRegister, .auxARegister]
        case .`normalize`: [.targetRegister, .operandRegister]
        case .`normalize2D`: [.targetRegister, .leftRegister, .rightRegister]
        case .`normalize3D`: [.targetRegister, .leftRegister, .rightRegister, .auxARegister]
        case .`dot`: [.targetRegister, .leftRegister, .rightRegister]
        case .`dot2D`: [.targetRegister, .leftRegister, .rightRegister, .auxARegister, .auxBRegister]
        case .`dot3D`: [.targetRegister, .leftRegister, .rightRegister, .auxARegister, .auxBRegister, .auxCRegister, .auxDRegister]
        case .`cross`: [.targetRegister, .leftRegister, .rightRegister]
        case .`cross2D`: [.targetRegister, .leftRegister, .rightRegister, .auxARegister, .auxBRegister]
        case .`cross3D`: [.targetRegister, .leftRegister, .rightRegister, .auxARegister, .auxBRegister, .auxCRegister, .auxDRegister]
        case .`angleBetween`: [.targetRegister, .leftRegister, .rightRegister]
        case .`angleBetween2D`: [.targetRegister, .leftRegister, .rightRegister, .auxARegister, .auxBRegister]
        case .`angleBetween3D`: [.targetRegister, .leftRegister, .rightRegister, .auxARegister, .auxBRegister, .auxCRegister, .auxDRegister]
        case .`takeFirst`: [.targetRegister, .sourceRegister, .countImmediate]
        case .`dropFirst`: [.targetRegister, .sourceRegister, .countImmediate]
        case .`takeLast`: [.targetRegister, .sourceRegister, .countImmediate]
        case .`dropLast`: [.targetRegister, .sourceRegister, .countImmediate]
        case .`takeHighest`: [.targetRegister, .sourceRegister, .countImmediate]
        case .`takeLowest`: [.targetRegister, .sourceRegister, .countImmediate]
        case .`dropHighest`: [.targetRegister, .sourceRegister, .countImmediate]
        case .`dropLowest`: [.targetRegister, .sourceRegister, .countImmediate]
        case .`oneRandom`: [.targetRegister, .sourceRegister]
        case .`takeRandom`: [.targetRegister, .sourceRegister, .countImmediate]
        case .`oneWeighted`: [.targetRegister, .sourceRegister, .weightRegister]
        case .`takeWeighted`: [.targetRegister, .sourceRegister, .countImmediate, .weightRegister]
        case .`count`: [.targetRegister, .operandRegister]
        case .`startsWith`: [.targetRegister, .leftRegister, .rightRegister]
        case .`endsWith`: [.targetRegister, .leftRegister, .rightRegister]
        case .`contains`: [.targetRegister, .leftRegister, .rightRegister]
        case .`containsAny`: [.targetRegister, .leftRegister, .rightRegister]
        case .`containsAll`: [.targetRegister, .leftRegister, .rightRegister]
        case .`containsValue`: [.targetRegister, .leftRegister, .rightRegister]
        case .`union`: [.targetRegister, .leftRegister, .rightRegister]
        case .`intersect`: [.targetRegister, .leftRegister, .rightRegister]
        case .`zip`: [.targetRegister, .leftRegister, .rightRegister]
        case .`keysOfMap`: [.targetRegister, .operandRegister]
        case .`valuesOfMap`: [.targetRegister, .operandRegister]
        case .`entriesOfMap`: [.targetRegister, .operandRegister]
        case .`first`: [.targetRegister, .operandRegister]
        case .`last`: [.targetRegister, .operandRegister]
        case .`single`: [.targetRegister, .operandRegister]
        case .`iteratorCreate`: [.targetRegister, .collectionRegister]
        case .`iteratorCreateOrJump`: [.targetRegister, .collectionRegister, .jumpTarget]
        case .`iteratorNext`: [.targetRegister, .iteratorRegister, .jumpTarget]
        case .`iteratorClose`: [.iteratorRegister]
        case .`hasAny`: [.targetRegister, .sourceRegister]
        case .`hasAll`: [.targetRegister, .sourceRegister]
        case .`distinct`: [.targetRegister, .sourceRegister]
        case .`sortAscending`: [.targetRegister, .sourceRegister]
        case .`sortDescending`: [.targetRegister, .sourceRegister]
        case .`reverse`: [.targetRegister, .sourceRegister]
        case .`shuffle`: [.targetRegister, .sourceRegister]
        case .`listBuilderCreate`: [.targetRegister]
        case .`listBuilderAdd`: [.builderRegister, .itemRegister]
        case .`listBuilderFinish`: [.targetRegister, .builderRegister]
        case .`mapBuilderCreate`: [.targetRegister]
        case .`mapBuilderAdd`: [.builderRegister, .keyRegister, .valueRegister]
        case .`mapBuilderFinish`: [.targetRegister, .builderRegister]
        case .`distinctBuilderCreate`: [.targetRegister]
        case .`distinctBuilderAdd`: [.builderRegister, .keyRegister, .valueRegister]
        case .`distinctBuilderFinish`: [.targetRegister, .builderRegister]
        case .`groupBuilderCreate`: [.targetRegister]
        case .`groupBuilderAdd`: [.builderRegister, .keyRegister, .valueRegister]
        case .`groupBuilderFinish`: [.targetRegister, .builderRegister]
        case .`orderBuilderCreate`: [.targetRegister]
        case .`orderBuilderAdd`: [.builderRegister, .keyRegister, .valueRegister]
        case .`orderBuilderFinishAscending`: [.targetRegister, .builderRegister]
        case .`orderBuilderFinishDescending`: [.targetRegister, .builderRegister]
        }
    }
}
