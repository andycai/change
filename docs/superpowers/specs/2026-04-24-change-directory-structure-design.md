# Change 目录结构重构设计

## 概述

将 `Assets/Fun` 目录重命名为 `Assets/Change`，重构程序集结构，统一命名空间，简化模块组织。

## 目标结构

```
Assets/Change/
├── Editor/
│   ├── Change.Editor.asmdef
│   └── *.cs              (namespace Change.Editor)
├── Framework/
│   ├── Change.Framework.asmdef
│   ├── Cqrs/             (namespace Change.Framework)
│   ├── Pooling/          (namespace Change.Framework)
│   ├── Collections/      (namespace Change.Framework)
│   ├── Fsm/              (namespace Change.Framework)
│   ├── Logging/          (namespace Change.Framework, 抽象层)
│   └── Tests/
│       └── EditMode/
│           ├── Change.Framework.Tests.asmdef
│           └── *.cs      (namespace Change.Framework)
└── Runtime/
    ├── Change.Runtime.asmdef
    ├── Timer/             (namespace Change.Runtime)
    ├── Logging/           (namespace Change.Runtime, UnitySink/FileSink)
    └── Tests/
        ├── EditMode/
        │   ├── Change.Runtime.EditModeTests.asmdef
        │   └── *.cs      (namespace Change.Runtime)
        └── PlayMode/
            ├── Change.Runtime.PlayModeTests.asmdef
            └── *.cs      (namespace Change.Runtime)
```

## 程序集清单

| 程序集 | 类型 | 说明 |
|--------|------|------|
| `Change.Editor` | Editor | Editor 代码 |
| `Change.Framework` | Runtime | 引擎无关框架（Cqrs, Pooling, Collections, Fsm, Logging 抽象） |
| `Change.Framework.Tests` | EditMode | Framework EditMode 测试 |
| `Change.Runtime` | Runtime | Runtime 代码（Timer, Logging 实现） |
| `Change.Runtime.EditModeTests` | EditMode | Runtime EditMode 测试 |
| `Change.Runtime.PlayModeTests` | PlayMode | Runtime PlayMode 测试 |

共 **6 个程序集**。

## 重命名清单

### 目录重命名

| 原路径 | 新路径 |
|--------|--------|
| `Assets/Fun` | `Assets/Change` |
| `Assets/Fun/Framework/Runtime/Cqrs` | `Assets/Change/Framework/Cqrs` |
| `Assets/Fun/Framework/Runtime/Pooling` | `Assets/Change/Framework/Pooling` |
| `Assets/Fun/Framework/Runtime/Collections` | `Assets/Change/Framework/Collections` |
| `Assets/Fun/Framework/Runtime/Logging` | `Assets/Change/Framework/Logging` |
| `Assets/Fun/Framework/Fsm/Runtime` | `Assets/Change/Framework/Fsm` |
| `Assets/Fun/Runtime/Timer` | `Assets/Change/Runtime/Timer` |
| `Assets/Fun/Runtime/Logging` | `Assets/Change/Runtime/Logging` |
| `Assets/Fun/Framework/Tests/*` | `Assets/Change/Framework/Tests/EditMode/*` |
| `Assets/Fun/Runtime/Timer/Tests/*` | `Assets/Change/Runtime/Tests/PlayMode/*` |
| `Assets/Fun/Runtime/Logging/Tests/*` | `Assets/Change/Runtime/Tests/EditMode/*` |

### asmdef 重命名

| 原名称 | 新名称 |
|--------|--------|
| `Fun.Framework` | `Change.Framework` |
| `Fun.Framework.Tests` | `Change.Framework.Tests` |
| `Fun.Runtime` | `Change.Runtime` |
| `Fun.Runtime.Timer` | (合并到 Change.Runtime) |
| `Fun.Runtime.Timer.PlayModeTests` | `Change.Runtime.PlayModeTests` |
| `Fun.Runtime.Logging` | (合并到 Change.Runtime) |
| `Fun.Runtime.Logging.Tests` | `Change.Runtime.EditModeTests` |
| `Fun.Framework.Fsm` | (合并到 Change.Framework) |
| `Fun.Framework.Fsm.Tests` | (合并到 Change.Framework.Tests) |

### 命名空间重命名

| 原命名空间 | 新命名空间 |
|-----------|-----------|
| `Fun.Framework.*` | `Change.Framework.*` |
| `Fun.Runtime.*` | `Change.Runtime.*` |
| `Fun.Editor.*` | `Change.Editor.*` |

## asmdef 配置要点

### Change.Framework
```json
{
    "name": "Change.Framework",
    "rootNamespace": "Change.Framework",
    "references": [],
    "optionalUnityReferences": [],
    "includePlatforms": [],
    "excludePlatforms": []
}
```

### Change.Runtime
```json
{
    "name": "Change.Runtime",
    "rootNamespace": "Change.Runtime",
    "references": ["Change.Framework"],
    "optionalUnityReferences": [],
    "includePlatforms": [],
    "excludePlatforms": []
}
```

### Change.Framework.Tests / Change.Runtime.EditModeTests
```json
{
    "name": "Change.Framework.Tests",
    "rootNamespace": "Change.Framework",
    "references": ["Change.Framework", "UnityEngine.TestRunner"],
    "optionalUnityReferences": ["UnityEngine.TestRunner"],
    "includePlatforms": ["Editor"],
    "excludePlatforms": []
}
```

### Change.Runtime.PlayModeTests
```json
{
    "name": "Change.Runtime.PlayModeTests",
    "rootNamespace": "Change.Runtime",
    "references": ["Change.Runtime", "UnityEngine.TestRunner"],
    "optionalUnityReferences": ["UnityEngine.TestRunner"],
    "includePlatforms": ["WindowsStandalone", "MacStandalone", "LinuxStandalone"],
    "excludePlatforms": []
}
```

## 迁移步骤

1. 创建新目录结构
2. 移动所有源文件到新位置
3. 重写所有 asmdef 文件
4. 全局替换命名空间 (`Fun.→Change.`)
5. 全局替换 asmdef 引用
6. 删除旧目录
7. 更新 Unity 项目（重新导入）
8. 验证所有测试通过

## 设计决策

1. **单一命名空间优先** — 每个程序集所有代码使用同一 root namespace，简洁第一
2. **测试集中组织** — Framework 和 Runtime 的测试分别集中在 `Tests/` 下
3. **Logging 分层** — Framework/Logging 放抽象层，Runtime/Logging 放具体实现（UnitySink, FileSink）
4. **PlayMode 测试独立程序集** — Unity 要求 PlayMode 测试必须独立 asmdef
