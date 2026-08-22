import type { ChapterStepProps } from "../../registry/types";
import "./TaskCenter.css";

const taskShot = "/assets/screenshots/tasks.png";

const phases = ["目录与依赖", "下载", "校验", "备份", "写入"] as const;
const phaseCopy = [
  ["先把前置条件说清。", "目录与依赖先确认，后面的写入才值得开始。"],
  ["下载有进度，也有来处。", "已接收内容与总大小属于任务记录的一部分。"],
  ["校验，是写入前的门。", "安装器会检查压缩包结构、路径、体积与写入边界。"],
  ["覆盖之前，保留旧文件。", "更新或安装需要覆盖已有文件时，才会保留原始副本。"],
  ["写入之后，结果仍可追溯。", "失败会恢复旧文件；被修改的文件不会被轻易删除。"],
] as const;

function Frame({ children, className = "" }: { children: React.ReactNode; className?: string }) {
  return (
    <div className={`tc-frame ${className}`}>
      <div className="tc-frame-bar"><span /><span /><span /></div>
      {children}
    </div>
  );
}

function PhaseTrack({ active }: { active: number }) {
  return (
    <div className="tc-phase-track" aria-label="任务安装阶段">
      {phases.map((phase, index) => {
        const status = index < active ? "done" : index === active ? "current" : "waiting";
        return (
          <div className={`tc-phase is-${status}`} key={phase}>
            <span>0{index + 1}</span>
            <strong>{phase}</strong>
          </div>
        );
      })}
    </div>
  );
}

function PhaseScene({ active }: { active: number }) {
  const [title, description] = phaseCopy[active]!;
  return (
    <div className="tc-scene tc-phase-scene">
      <div className="tc-phase-copy">
        <div className="tc-kicker">INSTALLATION / TRACEABLE STEPS</div>
        <h2>{title}</h2>
        <p>{description}</p>
      </div>
      <div className="tc-phase-stage">
        <PhaseTrack active={active} />
        <div className="tc-phase-status"><i /> 当前阶段：<strong>{phases[active]}</strong></div>
        {active === 0 && <div className="tc-phase-note">准备完成后，任务才会进入下载。</div>}
        {active === 1 && <div className="tc-download-stream"><span /><span /><span /></div>}
        {active === 2 && <div className="tc-check-gate"><i /><span>安全边界</span><i /></div>}
        {active === 3 && <div className="tc-backup-file"><span>旧文件</span><i /><strong>备份副本</strong></div>}
        {active === 4 && <div className="tc-write-rule"><span>更新失败：恢复旧文件</span><span>卸载前：核对文件哈希</span></div>}
        <div className="tc-demo-label">流程示意 / 未执行实际安装</div>
      </div>
    </div>
  );
}

export default function TaskCenterChapter({ step }: ChapterStepProps) {
  if (step === 0) {
    return (
      <div className="tc-scene tc-real-task">
        <div className="tc-real-copy">
          <div className="tc-kicker">TASK CENTER / NO BLACK BOX</div>
          <h2>每一次操作，<em>都该有去处。</em></h2>
          <p>安装、更新和卸载不是点完就消失。它们应该留下开始、过程、结果和可恢复的上下文。</p>
          <div className="tc-kind-row"><span>安装</span><span>更新</span><span>卸载</span></div>
        </div>
        <Frame className="tc-task-shot">
          <img src={taskShot} alt="UMM 任务中心空状态页面" />
          <div className="tc-shot-label">REAL TASK CENTER / EMPTY STATE</div>
        </Frame>
      </div>
    );
  }

  if (step === 1) {
    return (
      <div className="tc-scene tc-task-model">
        <div className="tc-model-copy">
          <div className="tc-kicker">ONE OPERATION, ONE RECORD</div>
          <h2>进度之外，<em>还要说明发生了什么。</em></h2>
          <p>任务中心把状态、阶段、进度和历史放在同一条记录里，出问题时不必重新翻文件夹。</p>
        </div>
        <div className="tc-model-card">
          <div className="tc-model-head"><span>安装任务</span><b>进行中</b></div>
          <div className="tc-model-progress"><i /><span>当前进度</span></div>
          <div className="tc-model-grid">
            <div><span>已接收 / 总大小</span><strong>下载进度</strong></div>
            <div><span>当前阶段</span><strong>处理状态</strong></div>
            <div><span>尝试次数</span><strong>操作历史</strong></div>
            <div><span>失败时保留</span><strong>失败原因</strong></div>
          </div>
          <div className="tc-model-foot"><i /> 任务结构示意，不执行实际安装</div>
        </div>
      </div>
    );
  }

  if (step === 2) return <PhaseScene active={0} />;
  if (step === 3) return <PhaseScene active={1} />;
  if (step === 4) return <PhaseScene active={2} />;
  if (step === 5) return <PhaseScene active={3} />;
  if (step === 6) return <PhaseScene active={4} />;

  if (step === 7) {
    return (
      <div className="tc-scene tc-recovery">
        <div className="tc-recovery-copy">
          <div className="tc-kicker">FAILURE SHOULD STAY EXPLAINABLE</div>
          <h2>失败不会被吞掉。</h2>
          <p>本次启动中，失败任务可以重试；重启后保留历史与失败原因，新的操作从插件详情重新发起。</p>
          <div className="tc-retry-flow"><span>失败原因</span><i /><span>尝试次数</span><i /><strong>本次会话重试</strong></div>
          <div className="tc-recovery-note">通知示意 / 未执行实际安装</div>
        </div>
        <div className="tc-notice-stack" aria-label="右下角动态通知示意">
          <div className="tc-notice tc-notice-login"><i /><div><span>账户</span><strong>登录状态已更新</strong></div></div>
          <div className="tc-notice tc-notice-env"><i /><div><span>环境</span><strong>插件环境已切换</strong></div></div>
          <div className="tc-notice tc-notice-install"><i /><div><span>安装与诊断</span><strong>任务结果已记录</strong></div></div>
          <div className="tc-notice-limit">同屏最多三条；新的通知会替换最早条目。</div>
        </div>
      </div>
    );
  }

  return null;
}
