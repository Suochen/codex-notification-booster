const messages = {
  zh: {
    "nav.github": "GitHub",
    "hero.eyebrow": "Windows 11 托盘通知增强器",
    "hero.title": "让 Codex 完成任务时真正提醒你。",
    "hero.lede": "当你在听音乐、看视频或专注别的窗口时，Codex 完成通知很容易被错过。这个小工具会为 Codex 通知播放更明显的提示音，并可短暂降低其他应用音量。",
    "hero.download": "下载 Windows x64 zip",
    "hero.release": "查看 Release",
    "hero.note": "portable 自包含版本，解压后运行，不需要另外安装 .NET。",
    "visual.codex": "Codex 完成任务",
    "visual.toast": "Windows 通知出现",
    "visual.sound": "提示音更明显",
    "why.label": "设计理由",
    "why.title": "不是替换 Codex，只是补上容易错过的提醒。",
    "why.body": "Codex 自己会发 Windows 通知，但在播放音乐时，系统通知声音可能不响、不明显，或者被其他声音盖过去。Codex Notification Booster 监听通知中心里的 Codex 通知，再由自己的进程播放提示音。",
    "features.listen.title": "监听 Codex 通知",
    "features.listen.body": "读取 Windows 暴露的通知元数据，识别 Codex 通知，不监控 Codex 进程。",
    "features.sound.title": "额外提示音",
    "features.sound.body": "识别到 Codex 通知后，由工具自己的进程播放更明显的提示音。",
    "features.duck.title": "音频闪避",
    "features.duck.body": "可在提示音播放时短暂降低其他应用音量，然后自动恢复。",
    "features.startup.title": "可选开机自启",
    "features.startup.body": "默认关闭。开启后写入当前用户 HKCU Run 启动项，关闭时会删除。",
    "steps.label": "使用方法",
    "steps.title": "下载，解压，打开 Codex 通知选项。",
    "steps.one": "下载 GitHub Release 里的 zip。",
    "steps.two": "解压整个 zip，打开 `win-x64` 文件夹。",
    "steps.three": "在 Codex 设置中，把“轮次完成通知”设为“始终”。",
    "steps.four": "双击 `CodexNotificationBooster.exe`，然后在系统托盘里右键打开菜单。",
    "privacy.label": "隐私和边界",
    "privacy.title": "本地运行，不上传通知内容。",
    "privacy.one": "日志保存在 `%LOCALAPPDATA%\\CodexNotificationBooster\\logs`。",
    "privacy.two": "正常日志不记录原始通知标题、正文、文本行或 XML。",
    "privacy.three": "不会修改系统主音量，不会提高 Codex 应用音量。",
    "privacy.four": "不创建 Windows 服务，不默认开机自启。",
    "cta.title": "让 Codex 完成时别再被你错过。",
    "cta.download": "下载最新版"
  },
  en: {
    "nav.github": "GitHub",
    "hero.eyebrow": "Windows 11 tray notification booster",
    "hero.title": "Make Codex completion notifications noticeable.",
    "hero.lede": "When music is playing or your attention is in another window, Codex completion notifications are easy to miss. This helper plays a clearer sound for Codex notifications and can briefly lower other app audio.",
    "hero.download": "Download Windows x64 zip",
    "hero.release": "View Release",
    "hero.note": "Self-contained portable build. Unzip and run; no separate .NET install needed.",
    "visual.codex": "Codex finishes work",
    "visual.toast": "Windows notification appears",
    "visual.sound": "Clearer helper sound",
    "why.label": "Why it exists",
    "why.title": "It does not replace Codex. It makes missed notifications harder to miss.",
    "why.body": "Codex already sends Windows notifications, but while music is playing the normal notification sound may be missing, quiet, or covered by other audio. Codex Notification Booster watches Codex notifications in the Windows notification center and plays its own helper sound.",
    "features.listen.title": "Watches Codex notifications",
    "features.listen.body": "Reads notification metadata exposed by Windows to identify Codex notifications. It does not monitor the Codex process.",
    "features.sound.title": "Extra helper sound",
    "features.sound.body": "When a Codex notification is detected, the helper plays a clearer sound from its own process.",
    "features.duck.title": "Audio ducking",
    "features.duck.body": "Optionally lowers other app audio briefly while the helper sound plays, then restores it.",
    "features.startup.title": "Optional startup",
    "features.startup.body": "Off by default. When enabled, it writes a current-user HKCU Run entry and removes it when disabled.",
    "steps.label": "How to use",
    "steps.title": "Download, unzip, and turn on Codex completion notifications.",
    "steps.one": "Download the zip from GitHub Releases.",
    "steps.two": "Unzip the whole archive and open the `win-x64` folder.",
    "steps.three": "In Codex settings, set “Turn completion notifications” to “Always”.",
    "steps.four": "Double-click `CodexNotificationBooster.exe`, then right-click the tray icon to open the menu.",
    "privacy.label": "Privacy and boundaries",
    "privacy.title": "Runs locally. Does not upload notification content.",
    "privacy.one": "Logs are stored at `%LOCALAPPDATA%\\CodexNotificationBooster\\logs`.",
    "privacy.two": "Normal logs do not record raw notification titles, bodies, text lines, or XML.",
    "privacy.three": "Does not change Windows master volume or raise Codex app volume.",
    "privacy.four": "Does not create a Windows service and does not enable startup by default.",
    "cta.title": "Stop missing Codex when it finishes.",
    "cta.download": "Download latest release"
  }
};

const preferred = localStorage.getItem("language") ||
  (navigator.language && navigator.language.toLowerCase().startsWith("zh") ? "zh" : "en");

function render(lang) {
  const copy = messages[lang] || messages.en;
  document.documentElement.lang = lang === "zh" ? "zh-CN" : "en";

  for (const node of document.querySelectorAll("[data-i18n]")) {
    const key = node.getAttribute("data-i18n");
    node.textContent = copy[key] || "";
  }

  for (const button of document.querySelectorAll("[data-lang]")) {
    button.setAttribute("aria-pressed", String(button.dataset.lang === lang));
  }

  localStorage.setItem("language", lang);
}

for (const button of document.querySelectorAll("[data-lang]")) {
  button.addEventListener("click", () => render(button.dataset.lang));
}

render(preferred);
