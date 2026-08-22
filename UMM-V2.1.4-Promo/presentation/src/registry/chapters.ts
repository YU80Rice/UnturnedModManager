import type { ChapterDef } from "./types";
import ColdopenChapter from "../chapters/01-coldopen/Coldopen";
import { narrations as coldopenNarrations } from "../chapters/01-coldopen/narrations";
import CommunityChapter from "../chapters/02-community/Community";
import { narrations as communityNarrations } from "../chapters/02-community/narrations";
import LocalControlChapter from "../chapters/03-local-control/LocalControl";
import { narrations as localControlNarrations } from "../chapters/03-local-control/narrations";
import TaskCenterChapter from "../chapters/04-task-center/TaskCenter";
import { narrations as taskCenterNarrations } from "../chapters/04-task-center/narrations";
import AccountUpdateChapter from "../chapters/05-account-update/AccountUpdate";
import { narrations as accountUpdateNarrations } from "../chapters/05-account-update/narrations";

/**
 * Order = order of presentation.
 *
 * Each chapter MUST provide a `narrations: Narration[]` array. Its length
 * is the chapter's step count — there is no `totalSteps` to maintain
 * separately. This guarantees the audio synthesis pipeline, the runtime
 * stepper, and the chapter `.tsx` switch on `step` cannot drift apart.
 *
 * Visual styling (color, fonts) comes entirely from the active theme —
 * chapters never hard-code palette / font names. See THEMES.md.
 */
export const CHAPTERS: ChapterDef[] = [
  {
    id: "coldopen",
    title: "把插件管理变简单",
    narrations: coldopenNarrations,
    Component: ColdopenChapter,
  },
  {
    id: "community",
    title: "从社区发现插件",
    narrations: communityNarrations,
    Component: CommunityChapter,
  },
  {
    id: "local-control",
    title: "把本地插件交还给玩家",
    narrations: localControlNarrations,
    Component: LocalControlChapter,
  },
  {
    id: "task-center",
    title: "每一次操作都有去处",
    narrations: taskCenterNarrations,
    Component: TaskCenterChapter,
  },
  {
    id: "account-update",
    title: "让启动器跟着你工作",
    narrations: accountUpdateNarrations,
    Component: AccountUpdateChapter,
  },
];
