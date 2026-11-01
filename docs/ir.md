# StoryGraph IR

StoryGraph 是编译器与图视图、HTML 导出之间的中间表示。它是语言的纯投影：不包含格式、注释和缩进。

---

## 1. 顶层结构

```json
{
  "schemaVersion": 1,
  "end": "END",
  "entry": "相信",
  "globals": {
    "trust_low": false,
    "has_key": false
  },
  "modules": []
}
```

| 字段 | 类型 | 说明 |
|---|---|---|
| `schemaVersion` | number | 当前为 `1` |
| `end` | string | 恒为 `"END"` |
| `entry` | string | 本次编译/导出选定的入口模块 |
| `globals` | object | 变量名到布尔默认值 |
| `modules` | Module[] | 模块列表，顺序与源文件一致 |

`entry` 不是语言结构，而是编译或演示时指定的起点。项目默认取第一个模块。

---

## 2. Module

```json
{
  "id": "相信",
  "title": "相信",
  "chapter": null,
  "characters": ["萧影", "影子"],
  "content": [],
  "exits": []
}
```

| 字段 | 类型 | 说明 |
|---|---|---|
| `id` | string | 模块 ID，等于 title（v0） |
| `title` | string | 显示标题 |
| `chapter` | string \| null | v0 恒为 null，为将来 `@chapter` 预留 |
| `characters` | string[] | 本模块声明的角色 |
| `content` | ContentBlock[] | 有序内容块 |
| `exits` | Exit[] | 有序出口分支 |

---

## 3. ContentBlock

内容块使用 `kind` 区分。

### 3.1 旁白

```json
{ "kind": "narration", "text": "雨水模糊了视线。" }
```

### 3.2 对话

```json
{ "kind": "dialogue", "speaker": "萧影", "text": "你来了。" }
```

### 3.3 图片

```json
{ "kind": "figure", "path": "assets/hospital.png" }
```

### 3.4 状态修改

```json
{ "kind": "set", "name": "trust_low", "value": true }
```

---

## 4. Exit

```json
{
  "character": "萧影",
  "condition": { "kind": "ref", "name": "trust_low" },
  "label": "相信他",
  "target": "医院",
  "effects": [
    { "name": "has_key", "value": true }
  ]
}
```

| 字段 | 类型 | 说明 |
|---|---|---|
| `character` | string \| null | 块级角色；null 表示自动模式 |
| `condition` | BoolExpr \| null | 条件；null 表示无条件 |
| `label` | string \| null | 选择文案；自动模式为 null |
| `target` | string | 目标模块 ID 或 `"END"` |
| `effects` | Assignment[] | 选中后执行的赋值 |

出口的顺序有意义：自动模式按数组顺序取第一条成立的。

---

## 5. BoolExpr

```json
{ "kind": "literal", "value": true }
{ "kind": "ref", "name": "trust_low" }
{ "kind": "not", "expr": { "kind": "ref", "name": "a" } }
{ "kind": "and", "left": {}, "right": {} }
{ "kind": "or", "left": {}, "right": {} }
```

- 只支持布尔字面量、变量引用、`not`、`and`、`or`。
- 没有比较和算术。

---

## 6. Assignment

```json
{ "name": "has_key", "value": true }
```

---

## 7. 完整示例（节选）

以下为节选，省略了部分模块。

```json
{
  "schemaVersion": 1,
  "end": "END",
  "entry": "相信",
  "globals": {
    "trust_low": false,
    "has_key": false
  },
  "modules": [
    {
      "id": "相信",
      "title": "相信",
      "chapter": null,
      "characters": ["萧影", "影子"],
      "content": [
        { "kind": "narration", "text": "雨水模糊了视线……" },
        { "kind": "dialogue", "speaker": "萧影", "text": "你来了。" },
        { "kind": "figure", "path": "assets/hospital.png" },
        { "kind": "set", "name": "trust_low", "value": true }
      ],
      "exits": [
        {
          "character": "萧影",
          "condition": { "kind": "ref", "name": "trust_low" },
          "label": "相信他",
          "target": "医院",
          "effects": [{ "name": "has_key", "value": true }]
        },
        {
          "character": "萧影",
          "condition": null,
          "label": "沉默",
          "target": "沉默",
          "effects": []
        }
      ]
    },
    {
      "id": "医院",
      "title": "医院",
      "chapter": null,
      "characters": ["萧影"],
      "content": [
        { "kind": "narration", "text": "两人走进医院。" }
      ],
      "exits": [
        {
          "character": null,
          "condition": { "kind": "ref", "name": "has_key" },
          "label": null,
          "target": "地下层",
          "effects": []
        },
        {
          "character": null,
          "condition": null,
          "label": null,
          "target": "走廊",
          "effects": []
        }
      ]
    }
  ]
}
```

---

## 8. 布局侧车

图布局坐标不属于 StoryGraph，单独存储，避免文本或语义变化冲掉手工排版。

```json
{
  "schemaVersion": 1,
  "positions": {
    "相信": { "x": 0, "y": 0 },
    "医院": { "x": 240, "y": 0 }
  }
}
```

- 键为模块 ID。
- 首次自动布局后写入；用户拖拽后覆盖对应节点。
- “重新布局”只重置没有手动坐标的节点。

---

## 9. 导出约定

- HTML 导出直接消费 StoryGraph。
- 图片按 `path` 解析并内嵌为 base64。
- 播放器只实现：文本推进、选择、条件、布尔状态、图片、结束。
- 单模块导出时，外部目标在运行时显示为演示结束或桩。
