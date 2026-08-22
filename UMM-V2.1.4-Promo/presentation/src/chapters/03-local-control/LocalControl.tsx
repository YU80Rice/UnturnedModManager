import type { ChapterStepProps } from "../../registry/types";
import "./LocalControl.css";

const localShot = "/assets/screenshots/local.png";

function PanelFrame({ children, className = "" }: { children: React.ReactNode; className?: string }) {
  return (
    <div className={`lc-frame ${className}`}>
      <div className="lc-frame-bar"><span /><span /><span /></div>
      {children}
    </div>
  );
}

function ProfileBoard({ name, current, className = "" }: { name: string; current: boolean; className?: string }) {
  return (
    <div className={`lc-profile ${current ? "is-current" : ""} ${className}`}>
      <span className="lc-profile-kind">插件方案</span>
      <strong>{name}</strong>
      <div className="lc-profile-states"><i /><i /><i /><i /></div>
      <small>{current ? "当前启停快照" : "可随时应用"}</small>
    </div>
  );
}

function TraceNode({ title, note, muted = false }: { title: string; note: string; muted?: boolean }) {
  return (
    <div className={`lc-trace-node${muted ? " is-muted" : ""}`}>
      <strong>{title}</strong>
      <small>{note}</small>
    </div>
  );
}

export default function LocalControlChapter({ step }: ChapterStepProps) {
  if (step === 0) {
    return (
      <div className="lc-scene lc-local-list">
        <div className="lc-list-copy">
          <div className="lc-kicker">LOCAL PLUGINS / PLAYER CONTROL</div>
          <h2>本地插件，<em>也该由玩家作主。</em></h2>
          <p>无论来自社区还是手动放入，UMM 都先把本地状态说清楚，再交给你启停或卸载。</p>
          <div className="lc-file-rail">
            <span>BepInEx/plugins</span><i /><strong>.dll</strong><i /><strong>.dll.disabled</strong>
          </div>
          <div className="lc-file-note"><b /> 真实目录扫描，不猜测文件状态</div>
        </div>
        <PanelFrame className="lc-local-shot">
          <img src={localShot} alt="UMM 本地插件页面" />
          <div className="lc-shot-label">REAL LOCAL PLUGINS SCREEN</div>
        </PanelFrame>
      </div>
    );
  }

  if (step === 1) {
    return (
      <div className="lc-scene lc-backtrack">
        <div className="lc-backtrack-copy">
          <div className="lc-kicker">SOURCE-AWARE NAVIGATION</div>
          <h2>从本地到社区，<em>也不会走丢。</em></h2>
          <p>匹配到社区条目时，详情页保留“从哪里来”的记录；返回，应该回到真正的上一层。</p>
        </div>
        <div className="lc-nav-stage" aria-label="本地插件到社区详情的导航历史">
          <div className="lc-nav-card lc-nav-local"><span>本地插件</span><strong>SteamP2PFriends</strong><small>社区匹配成功</small></div>
          <div className="lc-nav-path lc-nav-forward"><i /><b>打开详情</b></div>
          <div className="lc-nav-card lc-nav-detail"><span>社区详情</span><strong>版本 · 依赖 · 更新</strong><small>安装状态清晰可见</small></div>
          <div className="lc-nav-return"><i /><span>返回本地插件</span></div>
        </div>
        <div className="lc-history-note"><b /> 记录来源页面，而不是重置到首页</div>
      </div>
    );
  }

  if (step === 2) {
    return (
      <div className="lc-scene lc-profiles">
        <div className="lc-profiles-copy">
          <div className="lc-kicker">PLUGIN PROFILES</div>
          <h2>切换的是状态，<em>不是文件。</em></h2>
          <p>每个游戏目录都能保存自己的启停快照。想换一套玩法，不需要复制 DLL，也不用删掉已有插件。</p>
          <div className="lc-profile-rules"><span>不复制 DLL</span><span>不删除插件</span><span>不改写配置</span></div>
        </div>
        <div className="lc-profiles-stage">
          <ProfileBoard name="联机优化" current className="lc-profile-left" />
          <div className="lc-snapshot-core"><div className="lc-snapshot-ring" /><strong>启停<br />快照</strong><small>按目录保存</small></div>
          <ProfileBoard name="开发调试" current={false} className="lc-profile-right" />
          <div className="lc-snapshot-link lc-snapshot-link-left" />
          <div className="lc-snapshot-link lc-snapshot-link-right" />
        </div>
      </div>
    );
  }

  if (step === 3) {
    return (
      <div className="lc-scene lc-real-directory">
        <div className="lc-directory-copy">
          <div className="lc-kicker">DIRECTORY, NOT A VIRTUAL DISK</div>
          <h2>不挂虚拟盘，<em>直接管理真实目录。</em></h2>
          <p>Unity 对虚拟文件系统的读取并不总是可靠。应用插件方案前会预检目标文件；切换失败时按相反顺序回滚。</p>
        </div>
        <div className="lc-directory-stage">
          <div className="lc-direct-flow">
            <TraceNode title="UMM" note="应用插件方案" />
            <i />
            <TraceNode title="方案预检" note="验证全部目标文件" />
            <i />
            <TraceNode title="BepInEx/plugins" note="真实游戏目录" />
            <i />
            <TraceNode title="回滚" note="失败时反向恢复" />
          </div>
          <div className="lc-virtual-avoid"><span>WinFsp 虚拟盘</span><b>不采用</b></div>
        </div>
      </div>
    );
  }

  if (step === 4) {
    return (
      <div className="lc-scene lc-dxvk">
        <div className="lc-dxvk-copy">
          <div className="lc-kicker">DXVK / LOCAL DIAGNOSTICS</div>
          <h2>DXVK 提示，优先使用非虚拟适配器。</h2>
          <p>先排除远程桌面与常见虚拟显示驱动，再优先选择可识别的非虚拟适配器；结论和日志只导出到本地诊断包。</p>
        </div>
        <div className="lc-dxvk-stage">
          <div className="lc-gpu-source"><span>设备</span><strong>非虚拟适配器</strong><small>用于兼容性提示</small></div>
          <div className="lc-filter-stack"><span>跳过</span><strong>远程桌面</strong><strong>虚拟显示驱动</strong></div>
          <div className="lc-log-stack"><span>分析</span><strong>Unity</strong><strong>Unturned</strong><strong>BepInEx</strong><strong>DXVK</strong></div>
          <div className="lc-diagnostic-out"><span>导出</span><strong>本地诊断包</strong><small>不上传</small></div>
          <div className="lc-dxvk-link lc-link-one" /><div className="lc-dxvk-link lc-link-two" /><div className="lc-dxvk-link lc-link-three" />
        </div>
      </div>
    );
  }

  return null;
}
