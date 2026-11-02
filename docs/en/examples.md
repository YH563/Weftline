# Examples

[中文](../examples.md) | **English**

Two examples: a minimal demo without variables, and a complete example with conditions, effects, images, references, and automatic routing.

---

## 1. Minimal demo (no variables)

`.weft`:

```text
@chars
    @char 萧影

@scenes
    @scene 街道
    @scene 医院

@title 开场
    @scene 街道
    [雨水模糊了视线。]
    萧影:[你来了。]
@next(萧影):[继续] -> 医院
    ()[回头] -> 回头

@title 医院
    @scene 医院
    [两人走进医院。]
@next -> END

@title 回头
    @scene 街道
    [萧影转身离开。]
@next -> END
```

`.wstory`:

```text
@project 雨夜
    @summary [一个关于信任的故事。]

    @chars
        @char 萧影 [主角。]

    @scenes
        @scene 街道 [雨夜的街道。]
        @scene 医院 [雨夜里的医院。]
```

---

## 2. Complete example

### 2.1 project.wstory

```text
@project 雨夜
    @summary [一个关于信任的故事。]

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
@vars
    @var trust_low false
    @var has_key false

@chars
    @char 萧影
    @char 影子

@items
    @item 医院钥匙

@scenes
    @scene 医院

@title 序
    @scene 医院
    [雨夜，{萧影}走向医院。]
@next -> 相信 { @set trust_low true }

@title 相信
    @scene 医院
    [雨水模糊了视线，{萧影}在泥泞中奔跑。]
    萧影:[你来了。]
    影子:[别回头。]
    @fig "assets/hospital.png"
@next(萧影):(trust_low)[相信他] -> 医院 { @set has_key true }
    ()[沉默] -> 沉默

@title 医院
    @scene 医院
    [两人走进医院。]
@next():(has_key) -> 地下层
    () -> 走廊

@title 地下层
    @scene 医院
    [萧影找到了{医院钥匙}。]
@next -> END

@title 走廊
    @scene 医院
    [走廊空无一人。]
@next -> END

@title 沉默
    @scene 医院
    [萧影没有说话。]
@next -> END
```

---

## 3. Graph structure

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

- `序` sets `trust_low` through a branch effect.
- `相信` is choice mode: both options have labels, and the second is the unconditional fallback.
- `医院` is automatic mode: if `has_key` is true, go to `地下层`; otherwise `走廊`.
- `END` is the only terminal.

---

## 4. Walkthrough

### Path A: choose "相信他"

1. Enter `序`, apply `@set trust_low true`.
2. Enter `相信`, show narration, dialogue, and image.
3. Evaluate exits:
   - `(trust_low)[相信他]` is true, shown.
   - `()[沉默]` is unconditional, shown.
4. The player chooses "相信他".
5. Apply `@set has_key true`.
6. Enter `医院`.
7. `医院` routes automatically: `has_key` is true → `地下层`.
8. `地下层` → `END`.

### Path B: choose "沉默"

1. Enter `序`, set `trust_low`.
2. Enter `相信`, choose "沉默".
3. Enter `沉默` → `END`.

### Path C: `has_key` is false

1. Start from `相信` and choose "相信他".
2. If `has_key` is still `false`, enter `医院` and go to `走廊`.
3. `走廊` → `END`.

---

## 5. Covered language features

- `.weft` top-level `@vars` / `@chars` / `@items` / `@scenes` groups
- `.wstory` project info, summary, docs, descriptions
- `@title` module boundaries and `@scene` scene tags
- Inline `[]` narration and `character:[]` dialogue
- Text references `{name}`
- `@fig` image insertion
- Choice-mode exits (character, condition, label, fallback)
- Automatic-mode exits (condition + fallback)
- Branch effect blocks `{}`
- `@next -> END` termination
