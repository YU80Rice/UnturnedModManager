import type { ChapterStepProps } from "../../registry/types";
import "./AccountUpdate.css";

const mascot = "/assets/umm-mascot-chibi-v2.png";
const palettes = ["默认 Fluent", "暖米白 · UMM 蓝", "松林雾绿", "深海雾蓝", "夜雾紫", "克莱因蓝"];

function DemoMark({ children }: { children: React.ReactNode }) {
  return <div className="au-demo-mark">{children}</div>;
}

function ChainNode({ label, detail, active = false }: { label: string; detail: string; active?: boolean }) {
  return (
    <div className={`au-chain-node${active ? " is-active" : ""}`}>
      <span>{label}</span>
      <strong>{detail}</strong>
    </div>
  );
}

export default function AccountUpdateChapter({ step }: ChapterStepProps) {
  if (step === 0) {
    return (
      <div className="au-scene au-auth">
        <div className="au-auth-copy">
          <div className="au-kicker">ACCOUNT / BROWSER VERIFICATION</div>
          <h2>验证留在网页，<em>会话回到启动器。</em></h2>
          <p>登录时，人机验证在浏览器完成；UMM 只接收本机回调，再读取社区返回的身份和资料。</p>
          <DemoMark>匿名流程示意 / 不进行网页登录</DemoMark>
        </div>
        <div className="au-auth-stage" aria-label="社区账户登录流程示意">
          <div className="au-browser-card">
            <div className="au-window-bar"><i /><i /><i /><span>unmod.online</span></div>
            <strong>浏览器完成验证</strong>
            <div className="au-challenge-grid"><i /><i /><i /><i /><i /><i /><i /><i /><i /></div>
            <small>社区账户与人机验证</small>
          </div>
          <div className="au-auth-link"><i /><span>本机回调</span></div>
          <div className="au-callback-card">
            <span>localhost:52026</span>
            <strong>callback</strong>
            <small>仅接收已完成的会话</small>
          </div>
          <div className="au-account-card">
            <div className="au-avatar-placeholder"><i /></div>
            <div className="au-account-name"><strong>身份 · 用户名</strong><small>超出边栏时自动省略</small></div>
            <div className="au-account-ellipsis">...</div>
          </div>
        </div>
      </div>
    );
  }

  if (step === 1) {
    return (
      <div className="au-scene au-theme">
        <div className="au-theme-copy">
          <div className="au-kicker">THEME / SAVED PREFERENCES</div>
          <h2>不只换背景，<em>整套交互一起换。</em></h2>
          <p>浅色、深色和跟随系统会被保存；六套配色也会同步到按钮、开关、进度条、导航和焦点。</p>
          <div className="au-mode-row"><span>浅色</span><span>深色</span><strong>跟随系统</strong></div>
        </div>
        <div className="au-theme-stage">
          <div className="au-palette-grid" aria-label="UMM 配色方案">
            {palettes.map((palette, index) => <div className={`au-palette${index === 5 ? " is-current" : ""}`} key={palette}><i /><span>{palette}</span></div>)}
          </div>
          <div className="au-control-strip">
            <span className="au-button-sample">按钮</span>
            <span className="au-toggle-sample"><i />开关</span>
            <span className="au-progress-sample"><i />进度</span>
            <span className="au-nav-sample">导航</span>
            <span className="au-focus-sample">焦点</span>
          </div>
          <DemoMark>主题交互示意 / 不修改本机设置</DemoMark>
        </div>
      </div>
    );
  }

  if (step === 2) {
    return (
      <div className="au-scene au-update">
        <div className="au-update-copy">
          <div className="au-kicker">UPDATE / USER-CONFIRMED</div>
          <h2>发现新版本，<em>也由你决定什么时候更新。</em></h2>
          <p>欢迎区只检查官方 GitHub Release。下载前确认一次，校验完成后再确认一次；不静默替换 EXE。</p>
        </div>
        <div className="au-update-stage" aria-label="启动器更新保护流程示意">
          <ChainNode label="官方来源" detail="GitHub Release" active />
          <i className="au-chain-link" />
          <ChainNode label="资产检查" detail="HTTPS · 大小" active />
          <i className="au-chain-link" />
          <ChainNode label="完整性" detail="SHA-256" active />
          <i className="au-chain-link" />
          <ChainNode label="手动确认" detail="再决定安装" />
          <div className="au-backup-line"><span>当前 EXE</span><i /><strong>.bak 备份</strong></div>
          <DemoMark>更新流程示意 / 不下载或替换 EXE</DemoMark>
        </div>
      </div>
    );
  }

  if (step === 3) {
    return (
      <div className="au-scene au-architecture">
        <div className="au-architecture-copy">
          <div className="au-kicker">WPF / LAYERS WITH PURPOSE</div>
          <h2>学习成熟方法，<em>但不交出控制权。</em></h2>
          <p>UMM 保持 .NET 8 + WPF + WPF-UI 3.0.5 的稳定桌面基础，以 Pages、ViewModels、Services 分开界面、状态和实际操作。</p>
        </div>
        <div className="au-architecture-stage">
          <div className="au-layer-row"><div><span>界面</span><strong>Pages</strong></div><i /><div><span>状态</span><strong>ViewModels</strong></div><i /><div><span>能力</span><strong>Services</strong></div></div>
          <div className="au-learning-row"><div><span>向 UML 学习</span><strong>列表—详情 · 登录回调 · 导航历史</strong></div><div><span>UMM 的边界</span><strong>直接文件管理 · 安全检查 · 可恢复操作</strong></div></div>
          <div className="au-boundary-note"><i /> 不采用 WinFsp 虚拟盘，也不占用 <b>unmod://</b></div>
        </div>
      </div>
    );
  }

  if (step === 4) {
    return (
      <div className="au-scene au-closing">
        <div className="au-closing-stage" aria-label="UMM 产品定位总结">
          <div className="au-input-node au-input-workshop"><span>创意工坊</span><small>内容与玩法</small></div>
          <div className="au-input-node au-input-community"><span>插件社区</span><small>发现与更新</small></div>
          <div className="au-input-node au-input-local"><span>本地目录</span><small>启停与恢复</small></div>
          <div className="au-closing-core"><span>UMM</span><strong>Unturned Mod Manager</strong><small>插件环境控制台</small></div>
          <i className="au-close-link au-close-link-one" /><i className="au-close-link au-close-link-two" /><i className="au-close-link au-close-link-three" />
        </div>
        <div className="au-closing-copy">
          <div className="au-kicker">THE PLAYER KEEPS CONTROL</div>
          <h2>把插件环境，<em>整理清楚。</em></h2>
          <p>创意工坊赋予游戏血肉；UMM 让插件从哪里来、现在是什么状态、出了问题怎么回去，都仍然由玩家掌握。</p>
          <div className="au-closing-line"><i /> 发现 · 安装 · 启停 · 更新 · 卸载</div>
        </div>
        <img className="au-closing-mascot" src={mascot} alt="UMM Q版吉祥物" />
      </div>
    );
  }

  return null;
}
