import type { ChapterStepProps } from "../../registry/types";
import "./Community.css";

const communityShot = "/assets/screenshots/community-anon.png";
const detailShot = "/assets/screenshots/detail-final.png";

function Frame({ children, className = "" }: { children: React.ReactNode; className?: string }) {
  return (
    <div className={`cs-frame ${className}`}>
      <div className="cs-frame-bar"><span /><span /><span /></div>
      {children}
    </div>
  );
}

function DetailSignal({ name, note, accent = false }: { name: string; note: string; accent?: boolean }) {
  return (
    <div className={`cs-signal${accent ? " is-accent" : ""}`}>
      <i />
      <div><strong>{name}</strong><small>{note}</small></div>
    </div>
  );
}

type GuardState = "done" | "current" | "waiting";

function Guard({ index, title, subtitle, state }: { index: string; title: string; subtitle: string; state: GuardState }) {
  return (
    <div className={`cs-guard is-${state}`}>
      <span>{index}</span>
      <strong>{title}</strong>
      <small>{subtitle}</small>
    </div>
  );
}

const guardHeads = [
  ["依赖先说清。", "明确插件需要什么前置条件。"],
  ["目标不能越界。", "写入前先确认它只能去该去的位置。"],
  ["压缩包也有限制。", "大小和内容都需要留在可处理范围内。"],
  ["属于谁，才决定能否改动。", "更新与卸载先确认文件的来历和归属。"],
  ["最后核对，每一字节。", "SHA-256 让下载完整性有可验证的答案。"],
] as const;

function GuardrailScene({ activeIndex }: { activeIndex: number }) {
  const [title, summary] = guardHeads[activeIndex]!;
  const stateFor = (index: number): GuardState =>
    index < activeIndex ? "done" : index === activeIndex ? "current" : "waiting";

  return (
    <div className="cs-scene cs-guardrail">
      <div className="cs-guardrail-head">
        <div>
          <div className="cs-kicker">INSTALLATION IS A CHECKLIST</div>
          <h2>{title}</h2>
        </div>
        <p>{summary} 这是一条确认链，不是把压缩包直接扔进游戏目录。</p>
      </div>
      <div className="cs-guard-track" aria-label="插件安装保护流程">
        <Guard index="01" title="依赖解析" subtitle="确认前置条件" state={stateFor(0)} />
        <span className="cs-track-line" />
        <Guard index="02" title="路径检查" subtitle="拒绝越界写入" state={stateFor(1)} />
        <span className="cs-track-line" />
        <Guard index="03" title="压缩包限制" subtitle="限制可处理范围" state={stateFor(2)} />
        <span className="cs-track-line" />
        <Guard index="04" title="文件所有权" subtitle="知道什么能更新或卸载" state={stateFor(3)} />
        <span className="cs-track-line" />
        <Guard index="05" title="SHA-256" subtitle="校验下载完整性" state={stateFor(4)} />
      </div>
      <div className="cs-target"><i /> 目标目录：BepInEx/plugins <span>写入前完成确认</span></div>
    </div>
  );
}

export default function CommunityChapter({ step }: ChapterStepProps) {
  if (step === 0) {
    return (
      <div className="cs-scene cs-listing">
        <div className="cs-list-copy">
          <div className="cs-kicker">COMMUNITY / DISCOVER</div>
          <h2>先看条目，<em>再决定。</em></h2>
          <p>社区列表保持轻量。分类、排序和搜索条件变化后，会主动刷新结果。</p>
          <div className="cs-filter-strip" aria-label="社区筛选能力">
            <span>分类</span><b>+</b><span>排序</span><b>+</b><span>搜索</span>
          </div>
          <div className="cs-refresh-note"><i /> 条件变化，结果自动更新</div>
        </div>
        <Frame className="cs-list-shot">
          <img src={communityShot} alt="匿名化的 UMM 社区插件列表" />
          <div className="cs-shot-label">REAL COMMUNITY LIST / ACCOUNT REDACTED</div>
        </Frame>
        <div className="cs-list-count"><span>13</span><small>真实条目<br />来自社区结果</small></div>
      </div>
    );
  }

  if (step === 1) {
    return (
      <div className="cs-scene cs-detail">
        <div className="cs-detail-copy">
          <div className="cs-kicker">OPEN THE DETAIL</div>
          <h2>先读懂，<em>再安装。</em></h2>
          <p>一个详情页把插件的来历、版本和当前状态放到你面前。</p>
          <div className="cs-signal-stack">
            <DetailSignal name="作者与版本" note="知道它从哪里来" accent />
            <DetailSignal name="下载与依赖" note="判断安装前置条件" />
            <DetailSignal name="安装状态" note="区分可安装、已安装与可更新" />
          </div>
        </div>
        <Frame className="cs-detail-shot">
          <img src={detailShot} alt="UMM 插件详情界面" />
          <div className="cs-scan-line" />
          <div className="cs-shot-label">REAL PLUGIN DETAIL / PUBLIC COMMUNITY DATA</div>
        </Frame>
      </div>
    );
  }

  if (step === 2) {
    return (
      <div className="cs-scene cs-preview">
        <div className="cs-preview-copy">
          <div className="cs-kicker">IMAGE PREVIEW, WITH LIMITS</div>
          <h2>看原图，<em>不跑陌生脚本。</em></h2>
          <p>封面可放大、缩放和关闭；详情正文只把 HTTPS 图片带进本地预览链路。</p>
          <div className="cs-safe-route">
            <span>HTTPS 图片</span><i /><span>安全缩略图</span><i /><span>原图预览</span>
          </div>
          <div className="cs-script-block"><span>远程脚本</span><b>不执行</b></div>
        </div>
        <div className="cs-preview-stage">
          <div className="cs-preview-shadow" />
          <Frame className="cs-preview-shot">
            <img src={detailShot} alt="插件详情中的封面预览" />
          </Frame>
          <div className="cs-lens" aria-hidden="true"><i /><span>+</span></div>
          <div className="cs-preview-guide cs-guide-top" />
          <div className="cs-preview-guide cs-guide-bottom" />
        </div>
      </div>
    );
  }

  if (step === 3) return <GuardrailScene activeIndex={0} />;
  if (step === 4) return <GuardrailScene activeIndex={1} />;
  if (step === 5) return <GuardrailScene activeIndex={2} />;
  if (step === 6) return <GuardrailScene activeIndex={3} />;
  if (step === 7) return <GuardrailScene activeIndex={4} />;

  return null;
}
