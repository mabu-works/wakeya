"use strict";

// ===== 定数 =====
const SHUFFLE_DURATION = 1200; // シャッフル演出の時間（ミリ秒）
const SHUFFLE_INTERVAL = 50; // 名前が切り替わる間隔（ミリ秒）
const SAMPLE = ["田中", "佐藤", "鈴木", "高橋", "伊藤", "渡辺", "山本", "中村", "小林", "加藤", "吉田", "山田"];

// ===== 状態 =====
let splitting = false;
let lastGroups = []; // 直近の結果（グループの配列）

// ===== DOM =====
const $ = (id) => document.getElementById(id);
const participantsArea = $("participantsArea");
const participantCount = $("participantCount");
const groupCountInput = $("groupCount");
const groupSizeInput = $("groupSize");
const groupsControl = $("groupsControl");
const sizeControl = $("sizeControl");
const shuffleDisplay = $("shuffleDisplay");
const resultArea = $("resultArea");
const splitBtn = $("splitBtn");
const reshuffleBtn = $("reshuffleBtn");
const copyResultBtn = $("copyResultBtn");
const printBtn = $("printBtn");
const splitMsg = $("splitMsg");
const copyMsg = $("copyMsg");

// ===== リスト保存（localStorage） =====
// テーマキー wakeya-theme とは別に、入力と主要設定だけを wakeya-data に保存する
const STORAGE_KEY = "wakeya-data";

function saveData() {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify({
      participants: participantsArea.value,
      mode: currentMode(),
      groupCount: groupCountInput.value,
      groupSize: groupSizeInput.value,
    }));
  } catch (e) { /* 保存できない環境でも入力・分け自体は使える */ }
}

function clearSavedData() {
  try { localStorage.removeItem(STORAGE_KEY); } catch (e) { /* 削除できなくてもクリア自体は有効 */ }
}

function restoreData() {
  let raw = null;
  try { raw = localStorage.getItem(STORAGE_KEY); } catch (e) { return; }
  if (!raw) return;
  let data = null;
  try { data = JSON.parse(raw); } catch (e) { return; }
  if (!data || typeof data !== "object") return;
  if (typeof data.participants === "string") {
    participantsArea.value = data.participants;
  }
  if (data.mode === "groups" || data.mode === "size") {
    document.querySelectorAll('input[name="splitMode"]').forEach((radio) => {
      radio.checked = radio.value === data.mode;
    });
  }
  if (data.groupCount !== undefined && data.groupCount !== null && String(data.groupCount) !== "") {
    groupCountInput.value = String(data.groupCount);
  }
  if (data.groupSize !== undefined && data.groupSize !== null && String(data.groupSize) !== "") {
    groupSizeInput.value = String(data.groupSize);
  }
}

// ===== 参加者リスト =====
// 空行は無視する。同名でも別々の人として扱う（名簿の同姓同名対応のため重複を保持し、入力順を保つ）
function parseParticipants() {
  const result = [];
  participantsArea.value.split(/\r?\n/).forEach((line) => {
    const name = line.trim();
    if (name === "") return;
    result.push(name);
  });
  return result;
}

function updateCount() {
  participantCount.textContent = String(parseParticipants().length);
}

participantsArea.addEventListener("input", updateCount);
participantsArea.addEventListener("input", saveData);
groupCountInput.addEventListener("input", saveData);
groupCountInput.addEventListener("change", saveData);
groupSizeInput.addEventListener("input", saveData);
groupSizeInput.addEventListener("change", saveData);

$("clearBtn").addEventListener("click", () => {
  participantsArea.value = "";
  clearSavedData();
  updateCount();
  participantsArea.focus();
});

$("sampleBtn").addEventListener("click", () => {
  participantsArea.value = SAMPLE.join("\n");
  updateCount();
  saveData();
});

// ===== 分け方のモード =====
function currentMode() {
  const checked = document.querySelector('input[name="splitMode"]:checked');
  return checked ? checked.value : "groups";
}

function syncModeControls() {
  const mode = currentMode();
  groupsControl.hidden = mode !== "groups";
  sizeControl.hidden = mode !== "size";
}

document.querySelectorAll('input[name="splitMode"]').forEach((radio) => {
  radio.addEventListener("change", syncModeControls);
  radio.addEventListener("change", saveData);
});

// ===== グループ分け =====
function shuffle(list) {
  const arr = list.slice();
  for (let i = arr.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    const tmp = arr[i];
    arr[i] = arr[j];
    arr[j] = tmp;
  }
  return arr;
}

// グループ数 groupCount に対して、人数差が最大1になるよう均等に分ける
function splitIntoGroups(participants, groupCount) {
  const shuffled = shuffle(participants);
  const base = Math.floor(shuffled.length / groupCount);
  const remainder = shuffled.length % groupCount;
  const groups = [];
  let idx = 0;
  for (let g = 0; g < groupCount; g++) {
    const size = base + (g < remainder ? 1 : 0);
    groups.push(shuffled.slice(idx, idx + size));
    idx += size;
  }
  return groups;
}

// 設定を検証し、問題なければ {groupCount} を、問題があれば {error} を返す
function resolveGroupCount(participants) {
  if (participants.length === 0) {
    return { error: "参加者を1人以上入れてください。" };
  }
  if (participants.length < 2) {
    return { error: "グループ分けには参加者が2人以上必要です。" };
  }
  if (currentMode() === "groups") {
    const n = Number.parseInt(groupCountInput.value, 10);
    if (!Number.isFinite(n) || n < 1) {
      return { error: "グループ数は1以上で指定してください。" };
    }
    if (n > participants.length) {
      return { error: "参加者（" + participants.length + "人）よりグループ数が多いです。空のグループができてしまいます。" };
    }
    return { groupCount: n };
  }
  const size = Number.parseInt(groupSizeInput.value, 10);
  if (!Number.isFinite(size) || size < 1) {
    return { error: "1グループの人数は1人以上で指定してください。" };
  }
  if (size > participants.length) {
    return { error: "1グループの人数が参加者（" + participants.length + "人）を超えています。" };
  }
  return { groupCount: Math.ceil(participants.length / size) };
}

function split() {
  if (splitting) return;
  const participants = parseParticipants();
  const resolved = resolveGroupCount(participants);
  if (resolved.error) {
    splitMsg.textContent = resolved.error;
    return;
  }
  const groups = splitIntoGroups(participants, resolved.groupCount);
  animateAndFinish(participants, groups);
}

function animateAndFinish(pool, groups) {
  splitting = true;
  updateButtons();
  splitMsg.textContent = "";
  copyMsg.textContent = "";
  resultArea.innerHTML = "";
  shuffleDisplay.classList.add("is-shuffling");

  const startTime = performance.now();
  const timer = setInterval(() => {
    const elapsed = performance.now() - startTime;
    if (elapsed >= SHUFFLE_DURATION) {
      clearInterval(timer);
      shuffleDisplay.classList.remove("is-shuffling");
      splitting = false;
      finishSplit(groups);
      return;
    }
    const name = pool[Math.floor(Math.random() * pool.length)];
    shuffleDisplay.textContent = name;
  }, SHUFFLE_INTERVAL);
}

function finishSplit(groups) {
  lastGroups = groups.map((g) => g.slice());
  shuffleDisplay.textContent = "🎉 グループ分け完了（" + groups.length + "グループ）";

  const grid = document.createElement("div");
  grid.className = "group-grid";
  groups.forEach((members, i) => {
    const card = document.createElement("div");
    card.className = "group-card";
    const h = document.createElement("h3");
    h.textContent = "グループ" + (i + 1) + "（" + members.length + "人）";
    const ul = document.createElement("ul");
    members.forEach((name) => {
      const li = document.createElement("li");
      li.textContent = name;
      ul.appendChild(li);
    });
    card.appendChild(h);
    card.appendChild(ul);
    grid.appendChild(card);
  });
  resultArea.innerHTML = "";
  resultArea.appendChild(grid);
  updateButtons();
}

function updateButtons() {
  splitBtn.disabled = splitting;
  reshuffleBtn.disabled = splitting || lastGroups.length === 0;
  copyResultBtn.disabled = splitting || lastGroups.length === 0;
  if (printBtn) printBtn.disabled = splitting || lastGroups.length === 0;
}

splitBtn.addEventListener("click", split);
reshuffleBtn.addEventListener("click", split);

// ===== 結果コピー =====
function resultText() {
  const lines = ["わけや グループ分け結果"];
  lastGroups.forEach((members, i) => {
    lines.push("グループ" + (i + 1) + "（" + members.length + "人）：" + members.join("、"));
  });
  return lines.join("\n");
}

copyResultBtn.addEventListener("click", async () => {
  if (lastGroups.length === 0) return;
  const text = resultText();
  try {
    await navigator.clipboard.writeText(text);
    copyMsg.textContent = "結果をコピーしました。";
  } catch (e) {
    copyMsg.textContent = text;
  }
});

// ===== 結果の印刷 =====
// 印刷用CSS（@media print）で入力UIを隠し、グループ表だけを出す
if (printBtn) {
  printBtn.addEventListener("click", () => {
    if (lastGroups.length === 0) return;
    if (typeof window !== "undefined" && window && typeof window.print === "function") {
      window.print();
    }
  });
}

// ===== テーマ切り替え（sabasute と同じ initTheme パターン） =====
(function initTheme() {
  var root = document.documentElement;
  if (!root || !root.getAttribute || !root.setAttribute) return;
  function apply(theme) {
    root.setAttribute("data-theme", theme);
    var btn = document.getElementById("themeToggle");
    if (btn) btn.textContent = theme === "light" ? "☀️" : "🌙";
  }
  var current = root.getAttribute("data-theme");
  if (current !== "light" && current !== "dark") {
    try { current = localStorage.getItem("wakeya-theme"); } catch (e) { current = null; }
  }
  apply(current === "light" ? "light" : "dark");
  var btn = document.getElementById("themeToggle");
  if (btn && btn.addEventListener) {
    btn.addEventListener("click", function () {
      var next = root.getAttribute("data-theme") === "light" ? "dark" : "light";
      apply(next);
      try { localStorage.setItem("wakeya-theme", next); } catch (e) { /* 保存できなくても切り替え自体は有効 */ }
    });
  }
})();

// ===== 初期化 =====
restoreData();
updateCount();
syncModeControls();
updateButtons();
