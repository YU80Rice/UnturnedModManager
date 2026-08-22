import type { ChapterStepProps } from "../../registry/types";
import "./Coldopen.css";

const mascot = "/assets/umm-mascot-chibi-v2.png";
const homeShot = "/assets/screenshots/home-anon.png";

function GridMark({ className = "" }: { className?: string }) {
  return (
    <svg className={`co-grid-mark ${className}`} viewBox="0 0 180 180" aria-hidden="true">
      <path d="M90 8v164M8 90h164" />
      <circle cx="90" cy="90" r="48" />
      <circle cx="90" cy="90" r="7" />
      <path d="M90 8l-7 13m7-13 7 13M172 90l-13-7m13 7-13 7M90 172l-7-13m7 13 7-13M8 90l13-7M8 90l13 7" />
    </svg>
  );
}

function PipelineNode({ label, detail, active = false }: { label: string; detail: string; active?: boolean }) {
  return (
    <div className={`co-pipeline-node${active ? " is-active" : ""}`}>
      <span className="co-node-dot" />
      <strong>{label}</strong>
      <small>{detail}</small>
    </div>
  );
}

export default function ColdopenChapter({ step }: ChapterStepProps) {
  if (step === 0) {
    return (
      <div className="co-scene co-hero">
        <div className="co-hero-copy">
          <div className="co-kicker"><span className="co-live-dot" /> UMM / UNTURNED MOD MANAGER</div>
          <h1><span>让插件管理</span><em>轻松一点</em></h1>
          <p className="co-lead">一个启动器，也是你的插件环境控制台。</p>
          <div className="co-hero-meta"><span>V2.1.4</span><i /> <span>正式产品迭代</span></div>
        </div>
        <div className="co-hero-mascot-wrap">
          <GridMark className="co-hero-grid" />
          <div className="co-mascot-backdrop" />
          <img className="co-hero-mascot" src={mascot} alt="UMM Q版吉祥物" />
          <div className="co-hero-tag">启动 / 管理 / 恢复</div>
        </div>
        <div className="co-corner-label">CLICK TO EXPLORE <span>→</span></div>
      </div>
    );
  }

  if (step === 1) {
    return (
      <div className="co-scene co-milestone">
        <div className="co-section-kicker">FROM PROJECT TO PRODUCT</div>
        <div className="co-milestone-title"><span className="hero-num">2.0</span><span className="co-arrow">→</span><span className="hero-num co-cyan-num">2.1.4</span></div>
        <p className="co-section-lead">v2.0 把启动、社区、本地插件、账户和设置第一次交付成一个完整启动器。</p>
        <div className="co-timeline" aria-label="版本里程碑">
          <div className="co-timeline-line"><span /></div>
          <div className="co-timeline-item co-timeline-left"><span className="co-tick">2026.08.14</span><strong>正式发布</strong><small>完整产品边界</small></div>
          <div className="co-timeline-item co-timeline-right"><span className="co-tick">2026.08.16</span><strong>v2.1.4</strong><small>可读性与账户体验</small></div>
        </div>
        <div className="co-milestone-foot"><span className="co-accent-bar" /> 不是重新开始，是把稳定基础继续做完整。</div>
      </div>
    );
  }

  if (step === 2) {
    return (
      <div className="co-scene co-problem">
        <div className="co-section-kicker">THE OLD WAY</div>
        <h2>文件都在，但状态不在。</h2>
        <p className="co-section-lead">复制 DLL、对依赖、翻日志。每一步都发生了，却没有一个地方告诉你结果。</p>
        <div className="co-file-stage">
          <div className="co-folder co-folder-main"><span className="co-folder-glyph">DIR</span><strong>Unturned</strong><small>游戏根目录</small></div>
          <div className="co-file co-file-one"><span>.dll</span><b>BepInEx/plugins</b><small>?</small></div>
          <div className="co-file co-file-two"><span>.cfg</span><b>BepInEx/config</b><small>?</small></div>
          <div className="co-file co-file-three"><span>.log</span><b>logs/</b><small>?</small></div>
          <svg className="co-tangle" viewBox="0 0 700 330" aria-hidden="true"><path d="M155 160C255 74 318 250 394 142S535 70 588 196" /><path d="M155 160C259 222 324 112 394 214S523 248 588 196" /></svg>
        </div>
        <div className="co-problem-stamp">状态未知</div>
      </div>
    );
  }

  if (step === 3) {
    return (
      <div className="co-scene co-boundary">
        <div className="co-section-kicker">TWO LAYERS, ONE GAME</div>
        <h2>创意工坊给内容，UMM 管插件。</h2>
        <div className="co-boundary-grid">
          <div className="co-boundary-panel co-workshop-panel">
            <span className="co-panel-index">01</span><h3>Steam 创意工坊</h3>
            <div className="co-chip-row"><span>地图</span><span>武器</span><span>载具</span></div>
            <p>让 Unturned 拥有丰富的世界和玩法。</p>
          </div>
          <div className="co-boundary-bridge"><svg viewBox="0 0 190 120" aria-hidden="true"><path d="M8 60h174" /><path d="m154 32 28 28-28 28" /></svg><span>同一套游戏目录</span></div>
          <div className="co-boundary-panel co-umm-panel">
            <span className="co-panel-index">02</span><h3>UMM</h3>
            <div className="co-chip-row"><span>客户端插件</span><span>BepInEx</span><span>依赖</span></div>
            <p>把“怎么玩”变成看得见、可理解的选择。</p>
          </div>
        </div>
      </div>
    );
  }

  if (step === 4) {
    return (
      <div className="co-scene co-real-screen">
        <div className="co-real-copy"><div className="co-section-kicker">A CLEAR HOME SCREEN</div><h2>打开 UMM，先看到当前环境。</h2><p>哪里已安装、哪里停用、哪里可以修复，都有明确入口。</p><div className="co-callout-list"><span><i /> BepInEx 状态</span><span><i /> 全局模组环境</span><span><i /> DXVK 兼容性</span></div></div>
        <div className="co-shot-frame"><div className="co-shot-top"><span /> <span /> <span /></div><img src={homeShot} alt="匿名化的 UMM 游戏启动界面" /><div className="co-shot-caption">REAL UMM SCREEN / ACCOUNT REDACTED</div></div>
      </div>
    );
  }

  if (step === 5) {
    return (
      <div className="co-scene co-flow">
        <div className="co-section-kicker">READY WHEN YOU ARE</div>
        <h2>从目录到游戏，只留下一条清晰的路。</h2>
        <p className="co-section-lead">准备好 BepInEx 后，你可以启动模组模式；想回到原版，再切换回来。</p>
        <div className="co-pipeline">
          <PipelineNode label="游戏目录" detail="探测 / 选择" active />
          <span className="co-pipeline-link" />
          <PipelineNode label="插件环境" detail="BepInEx 5.4.23.5" active />
          <span className="co-pipeline-link" />
          <PipelineNode label="启动模式" detail="模组 / 原版" active />
          <span className="co-pipeline-link" />
          <PipelineNode label="插件入口" detail="社区 / 本地" />
        </div>
        <div className="co-flow-bottom"><span className="co-accent-2-dot" /> 发现 · 安装 · 启停 · 更新 · 卸载</div>
      </div>
    );
  }

  return null;
}
