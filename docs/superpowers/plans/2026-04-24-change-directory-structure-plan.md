# Change 目录结构重构实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 `Assets/Fun` 重构为 `Assets/Change`，统一程序集（6个）、命名空间（3个）、目录结构

**Architecture:** 将分散的 asmdef（Cqrs、Pooling、Collections、Fsm、Fsm.Tests、Timer、Logging 等）合并到 3 个主程序集：Change.Framework、Change.Runtime、Change.Editor。Framework 模块从 `Framework/Runtime/` 提升到 `Framework/` 根下。测试集中到 `Tests/` 子目录下。

**Tech Stack:** Unity, C#, asmdef

---

## 文件结构映射

### 需要移动的源文件

| 原路径 | 新路径 |
|--------|--------|
| `Fun/Framework/Runtime/Cqrs/*` | `Change/Framework/Cqrs/` |
| `Fun/Framework/Runtime/Pooling/*` | `Change/Framework/Pooling/` |
| `Fun/Framework/Runtime/Collections/*` | `Change/Framework/Collections/` |
| `Fun/Framework/Runtime/Logging/*` | `Change/Framework/Logging/` |
| `Fun/Framework/Fsm/Runtime/*` | `Change/Framework/Fsm/` |
| `Fun/Framework/Tests/EditMode/*` | `Change/Framework/Tests/EditMode/` |
| `Fun/Runtime/Timer/*` | `Change/Runtime/Timer/` |
| `Fun/Runtime/Logging/*` | `Change/Runtime/Logging/` |
| `Fun/Runtime/Timer/Tests/PlayMode/*` | `Change/Runtime/Tests/PlayMode/` |
| `Fun/Runtime/Logging/Tests/EditMode/*` | `Change/Runtime/Tests/EditMode/` |

### 需要创建的 asmdef 文件

| 文件 | 内容 |
|------|------|
| `Change/Editor/Change.Editor.asmdef` | 空程序集（Editor 代码后续添加） |
| `Change/Framework/Change.Framework.asmdef` | 合并所有 Framework 模块 |
| `Change/Framework/Tests/EditMode/Change.Framework.EditModeTests.asmdef` | Framework EditMode 测试 |
| `Change/Runtime/Change.Runtime.asmdef` | 合并所有 Runtime 模块 |
| `Change/Runtime/Tests/EditMode/Change.Runtime.EditModeTests.asmdef` | Runtime EditMode 测试 |
| `Change/Runtime/Tests/PlayMode/Change.Runtime.PlayModeTests.asmdef` | Runtime PlayMode 测试 |

### 需要删除的 asmdef

- `Fun.Framework.asmdef`
- `Fun.Framework.Fsm.asmdef`
- `Fun.Framework.Tests.asmdef`
- `Fun.Framework.Fsm.Tests.asmdef`
- `Fun.Runtime.Timer.asmdef`
- `Fun.Runtime.Timer.PlayModeTests.asmdef`
- `Fun.Runtime.Logging.asmdef`
- `Fun.Runtime.Logging.Tests.asmdef`

---

## 任务清单

### Task 1: 创建新目录结构

**Files:**
- Create: `UnityProject/Assets/Change/Editor/`
- Create: `UnityProject/Assets/Change/Framework/Cqrs/Abstractions/`
- Create: `UnityProject/Assets/Change/Framework/Cqrs/Core/`
- Create: `UnityProject/Assets/Change/Framework/Cqrs/Exceptions/`
- Create: `UnityProject/Assets/Change/Framework/Pooling/Internal/`
- Create: `UnityProject/Assets/Change/Framework/Collections/Abstractions/`
- Create: `UnityProject/Assets/Change/Framework/Collections/Containers/`
- Create: `UnityProject/Assets/Change/Framework/Collections/Core/`
- Create: `UnityProject/Assets/Change/Framework/Collections/Diagnostics/`
- Create: `UnityProject/Assets/Change/Framework/Fsm/`
- Create: `UnityProject/Assets/Change/Framework/Logging/`
- Create: `UnityProject/Assets/Change/Framework/Tests/EditMode/`
- Create: `UnityProject/Assets/Change/Runtime/Timer/`
- Create: `UnityProject/Assets/Change/Runtime/Logging/`
- Create: `UnityProject/Assets/Change/Runtime/Tests/EditMode/`
- Create: `UnityProject/Assets/Change/Runtime/Tests/PlayMode/`

- [ ] **Step 1: Create Change directory root**

Run: `mkdir -p UnityProject/Assets/Change/{Editor,Framework/{Cqrs/{Abstractions,Core,Exceptions},Pooling/Internal,Collections/{Abstractions,Containers,Core,Diagnostics},Fsm,Logging,Tests/EditMode},Runtime/{Timer,Logging,Tests/{EditMode,PlayMode}}}`

- [ ] **Step 2: Commit**

```bash
git add UnityProject/Assets/Change/ && git commit -m "chore: create Change directory structure

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 2: 移动 Framework 模块源文件

**Files:**
- Move: `Fun/Framework/Runtime/Cqrs/*` → `Change/Framework/Cqrs/`
- Move: `Fun/Framework/Runtime/Pooling/*` → `Change/Framework/Pooling/`
- Move: `Fun/Framework/Runtime/Collections/*` → `Change/Framework/Collections/`
- Move: `Fun/Framework/Runtime/Logging/*` → `Change/Framework/Logging/`
- Move: `Fun/Framework/Fsm/Runtime/*` → `Change/Framework/Fsm/`

- [ ] **Step 1: Move Cqrs module**

Run: `mv UnityProject/Assets/Fun/Framework/Runtime/Cqrs/* UnityProject/Assets/Change/Framework/Cqrs/`

- [ ] **Step 2: Move Pooling module**

Run: `mv UnityProject/Assets/Fun/Framework/Runtime/Pooling/* UnityProject/Assets/Change/Framework/Pooling/`

- [ ] **Step 3: Move Collections module**

Run: `mv UnityProject/Assets/Fun/Framework/Runtime/Collections/* UnityProject/Assets/Change/Framework/Collections/`

- [ ] **Step 4: Move Logging abstraction layer**

Run: `mv UnityProject/Assets/Fun/Framework/Runtime/Logging/* UnityProject/Assets/Change/Framework/Logging/`

- [ ] **Step 5: Move Fsm module**

Run: `mv UnityProject/Assets/Fun/Framework/Fsm/Runtime/* UnityProject/Assets/Change/Framework/Fsm/`

- [ ] **Step 6: Commit**

```bash
git add UnityProject/Assets/Change/Framework/ && git commit -m "chore: move Framework modules to new structure

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 3: 移动 Runtime 模块源文件

**Files:**
- Move: `Fun/Runtime/Timer/*` → `Change/Runtime/Timer/`
- Move: `Fun/Runtime/Logging/*` → `Change/Runtime/Logging/`

- [ ] **Step 1: Move Timer module**

Run: `mv UnityProject/Assets/Fun/Runtime/Timer/* UnityProject/Assets/Change/Runtime/Timer/`

- [ ] **Step 2: Move Logging implementation**

Run: `mv UnityProject/Assets/Fun/Runtime/Logging/* UnityProject/Assets/Change/Runtime/Logging/`

- [ ] **Step 3: Commit**

```bash
git add UnityProject/Assets/Change/Runtime/ && git commit -m "chore: move Runtime modules to new structure

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 4: 移动并重组测试文件

**Files:**
- Move: `Fun/Framework/Tests/EditMode/*` → `Change/Framework/Tests/EditMode/`
- Move: `Fun/Runtime/Timer/Tests/PlayMode/*` → `Change/Runtime/Tests/PlayMode/`
- Move: `Fun/Runtime/Logging/Tests/EditMode/*` → `Change/Runtime/Tests/EditMode/`
- Move: `Fun/Framework/Fsm/Tests/Editor/*` → `Change/Framework/Tests/EditMode/Fsm/`

- [ ] **Step 1: Move Framework EditMode tests**

Run: `mv UnityProject/Assets/Fun/Framework/Tests/EditMode/* UnityProject/Assets/Change/Framework/Tests/EditMode/`

- [ ] **Step 2: Move Fsm Editor tests to Framework/Tests/EditMode/Fsm**

Run: `mkdir -p UnityProject/Assets/Change/Framework/Tests/EditMode/Fsm && mv UnityProject/Assets/Fun/Framework/Fsm/Tests/Editor/* UnityProject/Assets/Change/Framework/Tests/EditMode/Fsm/`

- [ ] **Step 3: Move Runtime PlayMode tests**

Run: `mv UnityProject/Assets/Fun/Runtime/Timer/Tests/PlayMode/* UnityProject/Assets/Change/Runtime/Tests/PlayMode/`

- [ ] **Step 4: Move Runtime EditMode tests**

Run: `mv UnityProject/Assets/Fun/Runtime/Logging/Tests/EditMode/* UnityProject/Assets/Change/Runtime/Tests/EditMode/`

- [ ] **Step 5: Commit**

```bash
git add UnityProject/Assets/Change/ && git commit -m "chore: reorganize test files to centralized structure

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 5: 创建新的 asmdef 文件

**Files:**
- Create: `Change/Editor/Change.Editor.asmdef`
- Create: `Change/Framework/Change.Framework.asmdef`
- Create: `Change/Framework/Tests/EditMode/Change.Framework.EditModeTests.asmdef`
- Create: `Change/Runtime/Change.Runtime.asmdef`
- Create: `Change/Runtime/Tests/EditMode/Change.Runtime.EditModeTests.asmdef`
- Create: `Change/Runtime/Tests/PlayMode/Change.Runtime.PlayModeTests.asmdef`

- [ ] **Step 1: Create Change.Editor.asmdef**

```json
{
    "name": "Change.Editor",
    "rootNamespace": "Change.Editor",
    "references": [
        "Change.Framework",
        "Change.Runtime"
    ],
    "optionalUnityReferences": [],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": []
}
```

Run: Create file at `UnityProject/Assets/Change/Editor/Change.Editor.asmdef`

- [ ] **Step 2: Create Change.Framework.asmdef**

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

Run: Create file at `UnityProject/Assets/Change/Framework/Change.Framework.asmdef`

- [ ] **Step 3: Create Change.Framework.EditModeTests.asmdef**

```json
{
    "name": "Change.Framework.EditModeTests",
    "rootNamespace": "Change.Framework",
    "references": [
        "Change.Framework",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "optionalUnityReferences": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": []
}
```

Run: Create file at `UnityProject/Assets/Change/Framework/Tests/EditMode/Change.Framework.EditModeTests.asmdef`

- [ ] **Step 4: Create Change.Runtime.asmdef**

```json
{
    "name": "Change.Runtime",
    "rootNamespace": "Change.Runtime",
    "references": [
        "Change.Framework"
    ],
    "optionalUnityReferences": [],
    "includePlatforms": [],
    "excludePlatforms": []
}
```

Run: Create file at `UnityProject/Assets/Change/Runtime/Change.Runtime.asmdef`

- [ ] **Step 5: Create Change.Runtime.EditModeTests.asmdef**

```json
{
    "name": "Change.Runtime.EditModeTests",
    "rootNamespace": "Change.Runtime",
    "references": [
        "Change.Runtime",
        "Change.Framework",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "optionalUnityReferences": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": []
}
```

Run: Create file at `UnityProject/Assets/Change/Runtime/Tests/EditMode/Change.Runtime.EditModeTests.asmdef`

- [ ] **Step 6: Create Change.Runtime.PlayModeTests.asmdef**

```json
{
    "name": "Change.Runtime.PlayModeTests",
    "rootNamespace": "Change.Runtime",
    "references": [
        "Change.Runtime",
        "UnityEngine.TestRunner"
    ],
    "optionalUnityReferences": [
        "UnityEngine.TestRunner"
    ],
    "includePlatforms": [
        "WindowsStandalone",
        "MacStandalone",
        "LinuxStandalone"
    ],
    "excludePlatforms": []
}
```

Run: Create file at `UnityProject/Assets/Change/Runtime/Tests/PlayMode/Change.Runtime.PlayModeTests.asmdef`

- [ ] **Step 7: Commit**

```bash
git add UnityProject/Assets/Change/**/*.asmdef UnityProject/Assets/Change/**/*.asmdef.meta && git commit -m "chore: create new asmdef files for Change assemblies

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 6: 全局替换命名空间 Fun.* → Change.*

**Files:**
- Modify: All `*.cs` files under `UnityProject/Assets/Change/`

- [ ] **Step 1: Replace Fun.Framework.* → Change.Framework.* in all CS files**

Run: `find UnityProject/Assets/Change -name "*.cs" -exec sed -i '' 's/Fun\.Framework\./Change.Framework./g' {} \;`

- [ ] **Step 2: Replace Fun.Runtime.* → Change.Runtime.* in all CS files**

Run: `find UnityProject/Assets/Change -name "*.cs" -exec sed -i '' 's/Fun\.Runtime\./Change.Runtime./g' {} \;`

- [ ] **Step 3: Replace Fun.Editor.* → Change.Editor.* in all CS files (if any)**

Run: `find UnityProject/Assets/Change -name "*.cs" -exec sed -i '' 's/Fun\.Editor\./Change.Editor./g' {} \;`

- [ ] **Step 4: Replace root namespace Fun.Framework → Change.Framework in asmdef references**

Run: `find UnityProject/Assets/Change -name "*.asmdef" -exec sed -i '' 's/Fun\.Framework\./Change.Framework./g' {} \;`

- [ ] **Step 5: Replace root namespace Fun.Runtime → Change.Runtime in asmdef references**

Run: `find UnityProject/Assets/Change -name "*.asmdef" -exec sed -i '' 's/Fun\.Runtime\./Change.Runtime./g' {} \;`

- [ ] **Step 6: Commit**

```bash
git add UnityProject/Assets/Change/ && git commit -m "refactor: rename namespaces Fun.* to Change.*

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 7: 删除旧的 Fun 目录

**Files:**
- Delete: `UnityProject/Assets/Fun/`

- [ ] **Step 1: Verify all files have been moved before deletion**

Run: `find UnityProject/Assets/Fun -type f \( -name "*.cs" -o -name "*.asmdef" \) | wc -l`
Expected: 0

- [ ] **Step 2: Delete the old Fun directory**

Run: `rm -rf UnityProject/Assets/Fun`

- [ ] **Step 3: Commit deletion**

```bash
git add -A && git commit -m "chore: remove old Fun directory after migration

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 8: 验证 Unity 项目

**Files:**
- Test: Open Unity project and verify assembly definitions
- Test: Run all tests

- [ ] **Step 1: Open Unity and trigger reimport**

Open Unity project at `UnityProject/`. Wait for scripts to compile.

- [ ] **Step 2: Verify assembly list**

In Unity: Window → Assembly Definitions. Verify 6 assemblies:
- Change.Editor
- Change.Framework
- Change.Framework.EditModeTests
- Change.Runtime
- Change.Runtime.EditModeTests
- Change.Runtime.PlayModeTests

- [ ] **Step 3: Run all EditMode tests**

Run: `EditMode Tests` in Unity Test Runner
Expected: All pass

- [ ] **Step 4: Run PlayMode tests**

Run: `PlayMode Tests` in Unity Test Runner
Expected: All pass

- [ ] **Step 5: Commit verification**

```bash
git add -A && git commit -m "test: verify Change structure migration complete

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## 实施后验证清单

- [ ] 6 个 asmdef 文件存在且内容正确
- [ ] 所有命名空间已更改为 Change.*
- [ ] Framework 模块（Cqrs, Pooling, Collections, Fsm, Logging）在 `Change/Framework/` 下
- [ ] Runtime 模块（Timer, Logging 实现在 `Change/Runtime/` 下
- [ ] 测试文件在 `Change/Framework/Tests/EditMode/` 和 `Change/Runtime/Tests/` 下
- [ ] Unity 中编译无错误
- [ ] 所有 EditMode 测试通过
- [ ] 所有 PlayMode 测试通过
- [ ] 旧 Fun 目录已删除
