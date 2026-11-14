# 示例

[**中文**](./examples.md) | [English](./en/examples.md)

本文给出两个示例：一个最小 demo（无变量），一个包含条件、效果、图片、引用与自动路由的完整示例。

---

## 1. 最小 demo（无变量）

`.weft`：

```text
@title 第一章 街道

@char 萧影

@scene 街道
@scene 医院

@module 开场
    @scene 街道
    [雨水模糊了视线。]
    萧影:[你来了。]
@next(萧影):[继续] -> 医院
    ()[回头] -> 回头

@module 医院
    @scene 医院
    [两人走进医院。]
@next -> END

@module 回头
    @scene 街道
    [萧影转身离开。]
@next -> END
```

`.wstory`：

```text
@project 雨夜
    @summary [一个关于信任的故事。]

    @chapters
        @chapter 第一章 街道 [雨夜的开端。]

    @chars
        @char 萧影 [主角。]

    @scenes
        @scene 街道 [雨夜的街道。]
        @scene 医院 [雨夜里的医院。]
```

---

## 2. 完整示例

### 2.1 project.wstory

```text
@project 雨夜
    @summary [一个关于信任的故事。]

    @chapters
        @chapter 第一章 雨夜 [开篇。]

    @docs
        @doc 世界规则 [医院在雨夜不关门。]

    @chars
        @char 萧影 [主角。]
        @char 影子 [萧影的旧识。]

    @items
        @item 医院钥匙 [医院地下室的钥匙。]

    @scenes
        @scene 医院 [雨夜里的医院。]

    @vars
        @var trust_low false [萧影是否信任影子。]
        @var has_key false [是否拿到了医院钥匙。]
```

### 2.2 chapter1.weft

```text
@title 第一章 雨夜

@var trust_low false
@var has_key false

@char 萧影
@char 影子

@item 医院钥匙

@scene 医院

@module 序
    @scene 医院
    [雨夜，{萧影}走向医院。]
@next -> 相信 { @set trust_low true }

@module 相信
    @scene 医院
    [雨水模糊了视线，{萧影}在泥泞中奔跑。]
    萧影:[你来了。]
    影子:[别回头。]
    @fig "assets/hospital.png"
@next(萧影):(trust_low)[相信他] -> 医院 { @set has_key true }
    ()[沉默] -> 沉默

@module 医院
    @scene 医院
    [两人走进医院。]
@next():(has_key) -> 地下层
    () -> 走廊

@module 地下层
    @scene 医院
    [萧影找到了{医院钥匙}。]
@next -> END

@module 走廊
    @scene 医院
    [走廊空无一人。]
@next -> END

@module 沉默
    @scene 医院
    [萧影没有说话。]
@next -> END
```

---

## 3. 图结构

```text
            +--------+
            |  序    |
            +--------+
                 |  @set trust_low true
                 v
            +--------+
            |  相信  |
            +--------+
              |    |
     [相信他] |    | [沉默]
              v    v
          +------+ +------+
          | 医院 | | 沉默 |
          +------+ +------+
            |  |
   (has_key) |  | ()
            v  v
      +--------+ +------+
      | 地下层 | | 走廊 |
      +--------+ +------+
            \      /
             \    /
              v  v
             +-----+
             | END |
             +-----+
```

- `序` 通过分支效果把 `trust_low` 设为 true。
- `相信` 是选择模式：两个选项都有文案，第二个无条件作为兜底。
- `医院` 是自动模式：`has_key` 为真走地下层，否则走走廊。
- `END` 是唯一终点。

---

## 4. 演示走查

### 路径 A：选择“相信他”

1. 进入 `序`，出口效果 `@set trust_low true`。
2. 进入 `相信`，显示旁白、对话、图片。
3. 出口判定：
   - `(trust_low)[相信他]` 为真，显示。
   - `()[沉默]` 无条件，显示。
4. 玩家选择“相信他”。
5. 执行效果 `@set has_key true`。
6. 进入 `医院`。
7. `医院` 自动路由：`has_key` 为真 → `地下层`。
8. `地下层` → `END`。

### 路径 B：选择“沉默”

1. 进入 `序`，设置 `trust_low`。
2. 进入 `相信`，选择“沉默”。
3. 进入 `沉默` → `END`。

### 路径 C：`has_key` 为假

1. 从 `相信` 选择“相信他”。
2. 如果 `has_key` 仍为 `false`，进入 `医院` 后走 `走廊`。
3. `走廊` → `END`。

---

## 5. 覆盖的语言特性

- `.weft` 章节标题与顶部声明区（可直接声明，也可使用可选的 `@vars` / `@chars` / `@items` / `@scenes` 分组）
- `.wstory` 项目信息、概要、章节、设定文档、描述
- `@title` 章节边界、`@module` 模块边界与 `@scene` 场景切换标记
- 行内 `[]` 旁白与 `角色:[]` 对话
- 正文引用 `{名字}`
- `@fig` 图片插入
- 选择模式出口（角色、条件、文案、兜底）
- 自动模式出口（条件 + 兜底）
- 分支效果块 `{}`
- `@next -> END` 终止
