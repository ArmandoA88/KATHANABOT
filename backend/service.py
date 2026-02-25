from __future__ import annotations

import base64
import ctypes
import logging
import re
import threading
import time
from dataclasses import dataclass, field
from typing import Dict, List, Optional

import cv2
import numpy as np
import win32api
import win32con
import win32gui
import win32ui
from flask import Flask, jsonify, request
from PIL import ImageGrab

try:
    import pytesseract
except Exception:
    pytesseract = None

LOGGER = logging.getLogger("kathana_bot")
logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")

PW_CLIENTONLY = 0x1
PRIMARY_KEYS = ["1", "2", "3", "4", "5", "6", "7", "8", "9", "0"]
FUNCTION_KEYS = [f"F{idx}" for idx in range(1, 11)]
SUPPORTED_ACTION_KEYS = PRIMARY_KEYS + FUNCTION_KEYS


@dataclass
class Rect:
    x: int = 0
    y: int = 0
    w: int = 0
    h: int = 0

    @classmethod
    def from_dict(cls, payload: dict, fallback: "Rect") -> "Rect":
        if not payload:
            return fallback
        return cls(
            x=int(payload.get("x", fallback.x)),
            y=int(payload.get("y", fallback.y)),
            w=max(1, int(payload.get("w", fallback.w))),
            h=max(1, int(payload.get("h", fallback.h))),
        )


@dataclass
class ActionRule:
    key: str
    enabled: bool = True
    role: str = "attack"
    priority: int = 100
    cooldown_ms: int = 500
    trigger_percent: int = 40
    min_hp_percent: int = 1
    min_mp_percent: int = 1

    @classmethod
    def from_dict(cls, payload: dict, fallback: "ActionRule") -> "ActionRule":
        return cls(
            key=str(payload.get("key", fallback.key)).upper(),
            enabled=bool(payload.get("enabled", fallback.enabled)),
            role=str(payload.get("role", fallback.role)).lower(),
            priority=int(payload.get("priority", fallback.priority)),
            cooldown_ms=max(50, int(payload.get("cooldown_ms", fallback.cooldown_ms))),
            trigger_percent=max(1, min(99, int(payload.get("trigger_percent", fallback.trigger_percent)))),
            min_hp_percent=max(1, min(100, int(payload.get("min_hp_percent", fallback.min_hp_percent)))),
            min_mp_percent=max(1, min(100, int(payload.get("min_mp_percent", fallback.min_mp_percent)))),
        )


@dataclass
class BotConfig:
    window_title: str = "Kathana - The Coming of the Dark Ages"
    loop_ms: int = 80
    retarget_ms: int = 700
    mob_hp_presence_threshold: float = 3.0
    hp_bar: Rect = field(default_factory=lambda: Rect(20, 24, 220, 14))
    mp_bar: Rect = field(default_factory=lambda: Rect(20, 44, 220, 14))
    mob_name_rect: Rect = field(default_factory=lambda: Rect(700, 8, 300, 28))
    mob_hp_rect: Rect = field(default_factory=lambda: Rect(700, 34, 300, 14))
    denied_mobs: List[str] = field(default_factory=list)
    actions: List[ActionRule] = field(default_factory=list)

    @classmethod
    def default(cls) -> "BotConfig":
        default_actions = []
        for idx, key in enumerate(SUPPORTED_ACTION_KEYS):
            default_actions.append(
                ActionRule(
                    key=key,
                    enabled=key in PRIMARY_KEYS,
                    role="attack",
                    priority=(idx + 1) * 10,
                    cooldown_ms=500,
                    trigger_percent=40,
                    min_hp_percent=1,
                    min_mp_percent=1,
                )
            )
        return cls(actions=default_actions)

    @classmethod
    def from_dict(cls, payload: dict, fallback: "BotConfig") -> "BotConfig":
        denied_raw = payload.get("denied_mobs", fallback.denied_mobs)
        denied_mobs = [m.strip().lower() for m in denied_raw if str(m).strip()]

        fallback_actions = {item.key: item for item in fallback.actions}
        action_payloads = payload.get("actions", [])
        actions: List[ActionRule] = []
        for item in action_payloads:
            key = str(item.get("key", "")).strip().upper()
            if key not in fallback_actions:
                continue
            actions.append(ActionRule.from_dict(item, fallback_actions[key]))

        if not actions:
            actions = [ActionRule.from_dict({}, item) for item in fallback.actions]

        actions = sorted(actions, key=lambda x: x.priority)

        return cls(
            window_title=str(payload.get("window_title", fallback.window_title)),
            loop_ms=max(20, int(payload.get("loop_ms", fallback.loop_ms))),
            retarget_ms=max(100, int(payload.get("retarget_ms", fallback.retarget_ms))),
            mob_hp_presence_threshold=float(
                payload.get("mob_hp_presence_threshold", fallback.mob_hp_presence_threshold)
            ),
            hp_bar=Rect.from_dict(payload.get("hp_bar", {}), fallback.hp_bar),
            mp_bar=Rect.from_dict(payload.get("mp_bar", {}), fallback.mp_bar),
            mob_name_rect=Rect.from_dict(payload.get("mob_name_rect", {}), fallback.mob_name_rect),
            mob_hp_rect=Rect.from_dict(payload.get("mob_hp_rect", {}), fallback.mob_hp_rect),
            denied_mobs=denied_mobs,
            actions=actions,
        )


class WindowController:
    VK_MAP: Dict[str, int] = {
        "0": 0x30,
        "1": 0x31,
        "2": 0x32,
        "3": 0x33,
        "4": 0x34,
        "5": 0x35,
        "6": 0x36,
        "7": 0x37,
        "8": 0x38,
        "9": 0x39,
        "E": 0x45,
        "F1": win32con.VK_F1,
        "F2": win32con.VK_F2,
        "F3": win32con.VK_F3,
        "F4": win32con.VK_F4,
        "F5": win32con.VK_F5,
        "F6": win32con.VK_F6,
        "F7": win32con.VK_F7,
        "F8": win32con.VK_F8,
        "F9": win32con.VK_F9,
        "F10": win32con.VK_F10,
    }

    @staticmethod
    def find_window(window_title: str) -> int:
        hwnd = win32gui.FindWindow(None, window_title)
        if hwnd:
            return hwnd

        candidate: List[int] = []

        def _enum_handler(handle: int, _ctx: object) -> None:
            text = win32gui.GetWindowText(handle)
            if window_title.lower() in text.lower() and win32gui.IsWindowVisible(handle):
                candidate.append(handle)

        win32gui.EnumWindows(_enum_handler, None)
        return candidate[0] if candidate else 0

    @staticmethod
    def get_client_bbox(hwnd: int) -> Optional[tuple[int, int, int, int]]:
        try:
            left, top, right, bottom = win32gui.GetClientRect(hwnd)
            pt = win32gui.ClientToScreen(hwnd, (left, top))
            client_w = max(1, right - left)
            client_h = max(1, bottom - top)
            return pt[0], pt[1], client_w, client_h
        except Exception:
            return None

    @staticmethod
    def capture_client(hwnd: int) -> Optional[np.ndarray]:
        bbox = WindowController.get_client_bbox(hwnd)
        if bbox is None:
            return None

        _, _, width, height = bbox
        hwnd_dc = None
        mfc_dc = None
        save_dc = None
        bitmap = None

        try:
            hwnd_dc = win32gui.GetWindowDC(hwnd)
            mfc_dc = win32ui.CreateDCFromHandle(hwnd_dc)
            save_dc = mfc_dc.CreateCompatibleDC()

            bitmap = win32ui.CreateBitmap()
            bitmap.CreateCompatibleBitmap(mfc_dc, width, height)
            save_dc.SelectObject(bitmap)

            result = ctypes.windll.user32.PrintWindow(hwnd, save_dc.GetSafeHdc(), PW_CLIENTONLY)
            if result != 1:
                raise RuntimeError("PrintWindow failed")

            info = bitmap.GetInfo()
            data = bitmap.GetBitmapBits(True)
            image = np.frombuffer(data, dtype=np.uint8)
            image = image.reshape((info["bmHeight"], info["bmWidth"], 4))
            return cv2.cvtColor(image, cv2.COLOR_BGRA2BGR)
        except Exception:
            try:
                x, y, w, h = bbox
                fallback = ImageGrab.grab(bbox=(x, y, x + w, y + h))
                return cv2.cvtColor(np.array(fallback), cv2.COLOR_RGB2BGR)
            except Exception:
                return None
        finally:
            if bitmap is not None:
                win32gui.DeleteObject(bitmap.GetHandle())
            if save_dc is not None:
                save_dc.DeleteDC()
            if mfc_dc is not None:
                mfc_dc.DeleteDC()
            if hwnd_dc is not None:
                win32gui.ReleaseDC(hwnd, hwnd_dc)

    @staticmethod
    def post_key(hwnd: int, key: str, press_ms: int = 35) -> bool:
        key = key.upper()
        if key not in WindowController.VK_MAP:
            return False

        vk = WindowController.VK_MAP[key]
        scan = win32api.MapVirtualKey(vk, 0)
        lparam_down = 1 | (scan << 16)
        lparam_up = 1 | (scan << 16) | (1 << 30) | (1 << 31)

        try:
            win32gui.PostMessage(hwnd, win32con.WM_KEYDOWN, vk, lparam_down)
            time.sleep(max(5, press_ms) / 1000.0)
            win32gui.PostMessage(hwnd, win32con.WM_KEYUP, vk, lparam_up)
            return True
        except Exception:
            return False


def clamp_rect(image: np.ndarray, rect: Rect) -> Optional[np.ndarray]:
    h, w = image.shape[:2]
    x = max(0, min(w - 1, rect.x))
    y = max(0, min(h - 1, rect.y))
    rw = max(1, min(rect.w, w - x))
    rh = max(1, min(rect.h, h - y))
    if rw <= 0 or rh <= 0:
        return None
    return image[y : y + rh, x : x + rw]


def color_bar_percent(region: np.ndarray, bar_type: str) -> float:
    if region is None or region.size == 0:
        return 0.0

    hsv = cv2.cvtColor(region, cv2.COLOR_BGR2HSV)

    if bar_type == "hp":
        lower1 = np.array([0, 90, 70], dtype=np.uint8)
        upper1 = np.array([12, 255, 255], dtype=np.uint8)
        lower2 = np.array([165, 90, 70], dtype=np.uint8)
        upper2 = np.array([180, 255, 255], dtype=np.uint8)
        mask = cv2.inRange(hsv, lower1, upper1) | cv2.inRange(hsv, lower2, upper2)
    else:
        lower = np.array([90, 70, 50], dtype=np.uint8)
        upper = np.array([135, 255, 255], dtype=np.uint8)
        mask = cv2.inRange(hsv, lower, upper)

    col_profile = mask.mean(axis=0)
    indices = np.where(col_profile > 20)[0]
    if len(indices) == 0:
        return 0.0

    return float((indices.max() + 1) * 100.0 / region.shape[1])


def extract_mob_name(region: np.ndarray) -> str:
    if region is None or region.size == 0 or pytesseract is None:
        return ""

    gray = cv2.cvtColor(region, cv2.COLOR_BGR2GRAY)
    norm = cv2.normalize(gray, None, 0, 255, cv2.NORM_MINMAX)
    _, thr = cv2.threshold(norm, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)
    text = pytesseract.image_to_string(thr, config="--psm 7")
    cleaned = re.sub(r"[^A-Za-z0-9 '\\-]", "", text).strip()
    return cleaned


class BotEngine:
    def __init__(self) -> None:
        self._config = BotConfig.default()
        self._cfg_lock = threading.Lock()

        self._state_lock = threading.Lock()
        self._state = {
            "running": False,
            "window_found": False,
            "hp_percent": 0.0,
            "mp_percent": 0.0,
            "mob_name": "",
            "mob_hp_percent": 0.0,
            "target_valid": False,
            "last_action": "",
            "errors": "",
            "timestamp": time.time(),
        }

        self._stop_event = threading.Event()
        self._thread: Optional[threading.Thread] = None
        self._last_retarget = 0.0
        self._last_key_time: Dict[str, float] = {}

    def update_config(self, config: BotConfig) -> None:
        with self._cfg_lock:
            self._config = config

    def get_config(self) -> BotConfig:
        with self._cfg_lock:
            return self._config

    def get_status(self) -> dict:
        with self._state_lock:
            return dict(self._state)

    def _set_state(self, **kwargs: object) -> None:
        with self._state_lock:
            self._state.update(kwargs)
            self._state["timestamp"] = time.time()

    def start(self) -> None:
        if self._thread and self._thread.is_alive():
            return

        self._stop_event.clear()
        self._thread = threading.Thread(target=self._run, daemon=True)
        self._thread.start()
        self._set_state(running=True, errors="")

    def stop(self) -> None:
        self._stop_event.set()
        if self._thread and self._thread.is_alive():
            self._thread.join(timeout=2.0)
        self._set_state(running=False)

    def _choose_action(self, cfg: BotConfig, hp: float, mp: float, target_valid: bool) -> Optional[ActionRule]:
        ordered = [a for a in cfg.actions if a.enabled]
        ordered.sort(key=lambda a: a.priority)
        now = time.monotonic()

        def ready(action: ActionRule) -> bool:
            last = self._last_key_time.get(action.key, 0.0)
            return (now - last) * 1000.0 >= action.cooldown_ms

        for action in ordered:
            if action.role == "heal" and hp <= action.trigger_percent and ready(action):
                return action

        for action in ordered:
            if action.role == "mana" and mp <= action.trigger_percent and ready(action):
                return action

        if target_valid:
            for action in ordered:
                if action.role not in {"attack", "special"}:
                    continue
                if hp < action.min_hp_percent or mp < action.min_mp_percent:
                    continue
                if ready(action):
                    return action

        return None

    def _run(self) -> None:
        LOGGER.info("Bot loop started")
        while not self._stop_event.is_set():
            cfg = self.get_config()
            hwnd = WindowController.find_window(cfg.window_title)
            if not hwnd:
                self._set_state(
                    window_found=False,
                    hp_percent=0.0,
                    mp_percent=0.0,
                    mob_name="",
                    mob_hp_percent=0.0,
                    target_valid=False,
                    errors="Game window not found",
                )
                time.sleep(0.5)
                continue

            frame = WindowController.capture_client(hwnd)
            if frame is None:
                self._set_state(window_found=True, errors="Unable to capture game client")
                time.sleep(0.2)
                continue

            hp_region = clamp_rect(frame, cfg.hp_bar)
            mp_region = clamp_rect(frame, cfg.mp_bar)
            mob_name_region = clamp_rect(frame, cfg.mob_name_rect)
            mob_hp_region = clamp_rect(frame, cfg.mob_hp_rect)

            hp_percent = color_bar_percent(hp_region, "hp")
            mp_percent = color_bar_percent(mp_region, "mp")
            mob_hp_percent = color_bar_percent(mob_hp_region, "hp")
            mob_name = extract_mob_name(mob_name_region)
            normalized_name = mob_name.strip().lower()

            denied = normalized_name in cfg.denied_mobs if normalized_name else False
            target_valid = mob_hp_percent >= cfg.mob_hp_presence_threshold and not denied

            now = time.monotonic()
            if (not target_valid or denied) and (now - self._last_retarget) * 1000.0 >= cfg.retarget_ms:
                if WindowController.post_key(hwnd, "E"):
                    self._last_retarget = now
                    self._set_state(last_action="E (retarget)")

            action = self._choose_action(cfg, hp_percent, mp_percent, target_valid)
            if action and WindowController.post_key(hwnd, action.key):
                self._last_key_time[action.key] = time.monotonic()
                self._set_state(last_action=f"{action.key} ({action.role})")

            self._set_state(
                window_found=True,
                hp_percent=round(hp_percent, 1),
                mp_percent=round(mp_percent, 1),
                mob_name=mob_name,
                mob_hp_percent=round(mob_hp_percent, 1),
                target_valid=target_valid,
                errors="",
            )

            time.sleep(cfg.loop_ms / 1000.0)

        LOGGER.info("Bot loop stopped")


def capture_snapshot(window_title: str) -> Optional[str]:
    hwnd = WindowController.find_window(window_title)
    if not hwnd:
        return None

    frame = WindowController.capture_client(hwnd)
    if frame is None:
        return None

    ok, buffer = cv2.imencode(".png", frame)
    if not ok:
        return None

    return base64.b64encode(buffer.tobytes()).decode("ascii")


engine = BotEngine()
app = Flask(__name__)


@app.get("/health")
def health() -> tuple[str, int] | tuple[dict, int]:
    return jsonify({"ok": True}), 200


@app.get("/status")
def status() -> tuple[str, int] | tuple[dict, int]:
    return jsonify(engine.get_status()), 200


@app.post("/config")
def configure() -> tuple[str, int] | tuple[dict, int]:
    payload = request.get_json(silent=True) or {}
    cfg = engine.get_config()
    new_cfg = BotConfig.from_dict(payload, cfg)
    engine.update_config(new_cfg)
    return jsonify({"ok": True}), 200


@app.post("/start")
def start() -> tuple[str, int] | tuple[dict, int]:
    engine.start()
    return jsonify({"ok": True}), 200


@app.post("/stop")
def stop() -> tuple[str, int] | tuple[dict, int]:
    engine.stop()
    return jsonify({"ok": True}), 200


@app.get("/snapshot")
def snapshot() -> tuple[str, int] | tuple[dict, int]:
    cfg = engine.get_config()
    image = capture_snapshot(cfg.window_title)
    if not image:
        return jsonify({"ok": False, "error": "Unable to capture snapshot"}), 404
    return jsonify({"ok": True, "image_base64": image}), 200


if __name__ == "__main__":
    LOGGER.info("Starting API on http://127.0.0.1:57999")
    app.run(host="127.0.0.1", port=57999, debug=False)
