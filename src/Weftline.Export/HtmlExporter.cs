using System.Text;
using Weftline.Language.Ir;

namespace Weftline.Export;

public static class HtmlExporter
{
    public static string Export(StoryGraph graph)
    {
        var title = graph.Project?.Name ?? "Weftline Story";
        var json = StoryGraphJson.Serialize(graph).Replace("</", "<\\/", StringComparison.Ordinal);

        return Template
            .Replace("__TITLE__", Escape(title), StringComparison.Ordinal)
            .Replace("__GRAPH__", json, StringComparison.Ordinal);
    }

    private static string Escape(string value)
        => value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);

    private const string Template = """
    <!doctype html>
    <html lang="zh-CN">
    <head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>__TITLE__</title>
    <style>
    :root { color-scheme: dark; --bg:#111318; --panel:#1b1f27; --text:#e8eaf0; --muted:#9aa3b2; --accent:#7c9cff; --line:#2c3340; }
    * { box-sizing: border-box; }
    body { margin:0; min-height:100vh; background:radial-gradient(circle at 20% 0%, #24304a, var(--bg) 55%); color:var(--text); font-family: "Noto Sans SC", "PingFang SC", "Microsoft YaHei", system-ui, sans-serif; }
    main { max-width: 820px; margin: 0 auto; padding: 48px 24px 72px; }
    header { display:flex; align-items:baseline; justify-content:space-between; gap:16px; margin-bottom:20px; }
    h1 { font-size: 20px; margin:0; letter-spacing:.04em; }
    .chapter { color:var(--muted); font-size: 13px; }
    .card { background:linear-gradient(180deg, rgba(255,255,255,.035), rgba(255,255,255,.012)); border:1px solid var(--line); border-radius:18px; padding:28px; box-shadow:0 20px 60px rgba(0,0,0,.35); }
    .module { font-size:13px; color:var(--muted); margin-bottom:18px; letter-spacing:.08em; }
    .content p { line-height:1.9; margin:0 0 16px; font-size:16px; }
    .dialogue strong { color:var(--accent); margin-right:8px; }
    .ref { color:#8ee6c8; }
    .choices { display:grid; gap:10px; margin-top:24px; }
    button { appearance:none; border:1px solid #3a465c; background:#202736; color:var(--text); text-align:left; padding:13px 16px; border-radius:12px; font:inherit; cursor:pointer; transition:.16s ease; }
    button:hover { border-color:var(--accent); background:#263149; transform:translateY(-1px); }
    .meta { margin-top:20px; color:var(--muted); font-size:12px; }
    .footer { margin-top:18px; display:flex; justify-content:flex-end; }
    .ghost { background:transparent; color:var(--muted); border-color:var(--line); }
    .ending { text-align:center; padding:28px 0; }
    .ending h2 { margin:0 0 10px; font-size:22px; }
    </style>
    </head>
    <body>
    <main>
      <header><h1>__TITLE__</h1><span class="chapter" id="chapter"></span></header>
      <section class="card" id="story"></section>
      <div class="footer"><button class="ghost" id="restart" type="button">重新开始</button></div>
      <div class="meta" id="meta"></div>
    </main>
    <script>
    const graph = __GRAPH__;
    const state = { ...graph.globals };
    const story = document.getElementById('story');
    const chapter = document.getElementById('chapter');
    const meta = document.getElementById('meta');

    function escapeHtml(value) {
      return String(value ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
    }

    function evalExpr(expr) {
      if (!expr) return true;
      switch (expr.kind) {
        case 'literal': return !!expr.value;
        case 'ref': return !!state[expr.name];
        case 'not': return !evalExpr(expr.expr);
        case 'and': return evalExpr(expr.left) && evalExpr(expr.right);
        case 'or': return evalExpr(expr.left) || evalExpr(expr.right);
        default: return true;
      }
    }

    function segmentHtml(segment) {
      if (segment.kind === 'ref') return `<span class="ref">${escapeHtml(segment.name)}</span>`;
      return escapeHtml(segment.text ?? '');
    }

    function contentHtml(block) {
      if (block.kind === 'dialogue') {
        const text = (block.segments || []).map(segmentHtml).join('');
        return `<p class="dialogue"><strong>${escapeHtml(block.speaker)}</strong>${text}</p>`;
      }
      if (block.kind === 'figure') return `<p class="ref">[图] ${escapeHtml(block.path)}</p>`;
      const text = (block.segments || []).map(segmentHtml).join('');
      return `<p>${text}</p>`;
    }

    function applyEffects(exit) {
      for (const effect of exit.effects || []) state[effect.name] = effect.value;
    }

    function choose(exit) {
      applyEffects(exit);
      render(exit.target);
    }

    function render(id) {
      if (!id || id === graph.end) {
        chapter.textContent = '';
        story.innerHTML = '<div class="ending"><h2>故事结束</h2><p>感谢阅读。</p></div>';
        meta.textContent = `END · ${graph.modules.length} 个模块`;
        return;
      }

      const mod = graph.modules.find(m => m.id === id);
      if (!mod) {
        story.innerHTML = `<p class="ref">找不到模块：${escapeHtml(id)}</p>`;
        return;
      }

      chapter.textContent = mod.chapter || '';
      const exits = (mod.exits || []).filter(e => evalExpr(e.condition));
      const auto = exits.filter(e => !e.label);
      const choices = exits.filter(e => e.label);

      story.innerHTML = `<div class="module">@module ${escapeHtml(mod.title)}${mod.scene ? ` · ${escapeHtml(mod.scene)}` : ''}</div><div class="content">${(mod.content || []).map(contentHtml).join('')}</div>`;

      if (choices.length) {
        const box = document.createElement('div');
        box.className = 'choices';
        for (const exit of choices) {
          const button = document.createElement('button');
          button.type = 'button';
          button.textContent = exit.label;
          button.addEventListener('click', () => choose(exit));
          box.appendChild(button);
        }
        story.appendChild(box);
      } else if (auto.length) {
        choose(auto[0]);
        return;
      } else {
        story.innerHTML += '<p class="ref">没有可用的出口。</p>';
      }

      meta.textContent = `${mod.id} · ${state ? Object.entries(state).map(([k,v]) => `${k}=${v}`).join(', ') : ''}`;
    }

    document.getElementById('restart').addEventListener('click', () => {
      for (const key of Object.keys(state)) delete state[key];
      Object.assign(state, graph.globals);
      render(graph.entry);
    });

    render(graph.entry);
    </script>
    </body>
    </html>
    """;
}
