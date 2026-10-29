"""Admin sahifa testlari: haqiqiy brauzer (Chromium), Supabase o'rniga xotiradagi soxta server.

Ishga tushirish:  pip install pytest playwright && python -m playwright install chromium && python -m pytest admin/tests -q
"""
import json
import pathlib
import re
import uuid

import struct
import zlib

import pytest
from playwright.sync_api import Page, Route, expect, sync_playwright

ROOT = pathlib.Path(__file__).resolve().parents[1]
URL = "https://test.supabase.co"
KEY = "sb_publishable_test"
ADMIN = ("admin@maktab.uz", "togri-parol")
STRANGER = ("begona@maktab.uz", "parol123")
TODAY = "2026-10-29"


def real_png(w=8, h=4) -> bytes:
    """Haqiqiy (brauzer ko'rsata oladigan) PNG."""
    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)
    raw = b"".join(b"\x00" + b"\x00\x67\xc0" * w for _ in range(h))
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(raw)) + chunk(b"IEND", b""))


class FakeSupabase:
    """Supabase'ning admin sahifa ishlatadigan qismlari: auth, ads, rpc, storage."""

    def __init__(self):
        self.ads = []
        self.report = {}
        self.uploads = {}
        self.calls = []
        self.expire_next = False
        self.refreshed = 0

    def add(self, **kw):
        ad = {"id": str(uuid.uuid4()), "title": "R", "image_url": None, "link_url": None, "starts_on": None,
              "ends_on": None, "priority": 0, "is_active": True, "created_at": "2026-10-01T00:00:00Z"}
        ad.update(kw)
        self.ads.append(ad)
        return ad

    def handle(self, route: Route):
        req = route.request
        path = req.url[len(URL):]
        body = req.post_data_buffer
        headers = req.headers
        self.calls.append((req.method, path, headers, body))
        token = headers.get("authorization", "")

        def reply(status=200, data=None):
            route.fulfill(status=status, content_type="application/json", body="" if data is None else json.dumps(data))

        if path.startswith("/storage/v1/object/public/ad-images/"):
            # Ommaviy rasm: brauzerning <img> so'rovi — kalit kerak emas.
            name = path.rsplit("/", 1)[1]
            if name not in self.uploads:
                return reply(404, {"message": "not found"})
            return route.fulfill(status=200, content_type="image/png", body=real_png())
        assert headers.get("apikey") == KEY, "API so'rovlarida ommaviy kalit bo'lishi kerak"

        if path.startswith("/auth/v1/token?grant_type=password"):
            creds = json.loads(body)
            if (creds["email"], creds["password"]) == ADMIN:
                return reply(200, {"access_token": "admin-1", "refresh_token": "r-admin", "user": {"email": ADMIN[0]}})
            if (creds["email"], creds["password"]) == STRANGER:
                return reply(200, {"access_token": "begona", "refresh_token": "r-b", "user": {"email": STRANGER[0]}})
            return reply(400, {"error": "invalid_grant", "error_description": "Invalid login credentials"})
        if path.startswith("/auth/v1/token?grant_type=refresh_token"):
            self.refreshed += 1
            return reply(200, {"access_token": "admin-2", "refresh_token": "r-admin-2"})

        if self.expire_next and token == "Bearer admin-1":
            self.expire_next = False
            return reply(401, {"message": "JWT expired"})
        is_admin = token in ("Bearer admin-1", "Bearer admin-2")

        if path == "/rest/v1/rpc/is_admin":
            return reply(200, is_admin)
        if not is_admin:
            return reply(403, {"code": "42501", "message": "permission denied"})
        if path.startswith("/rest/v1/ads?select="):
            return reply(200, sorted(self.ads, key=lambda a: -a["priority"]))
        if path == "/rest/v1/rpc/ad_report":
            return reply(200, [dict(ad_id=k, **v) for k, v in self.report.items()])
        if path == "/rest/v1/ads" and req.method == "POST":
            row = json.loads(body)
            if row.get("link_url") and not row["link_url"].startswith("https://"):
                return reply(400, {"code": "23514", "message": "violates check constraint"})
            self.add(**row)
            return reply(201)
        m = re.match(r"/rest/v1/ads\?id=eq\.([0-9a-f-]+)$", path)
        if m:
            ad = next(a for a in self.ads if a["id"] == m.group(1))
            if req.method == "PATCH":
                ad.update(json.loads(body))
            elif req.method == "DELETE":
                self.ads.remove(ad)
            return reply(204)
        m = re.match(r"/storage/v1/object/ad-images/(.+)$", path)
        if m:
            if req.method == "POST":
                self.uploads[m.group(1)] = (headers.get("content-type"), body)
            elif req.method == "DELETE":
                self.uploads.pop(m.group(1), None)
            return reply(200, {"Key": "ad-images/" + m.group(1)})
        return reply(404, {"message": "noma'lum: " + path})


@pytest.fixture(scope="session")
def browser():
    with sync_playwright() as p:
        b = p.chromium.launch()
        yield b
        b.close()


@pytest.fixture()
def fake():
    return FakeSupabase()


def open_page(browser, fake, tmp_path, configured=True):
    html = (ROOT / "reklama-admin.html").read_text(encoding="utf-8")
    if configured:
        html = html.replace('"https://SIZNING-LOYIHA.supabase.co"', json.dumps(URL)).replace('"SIZNING-OMMAVIY-KALITINGIZ"', json.dumps(KEY))
    f = tmp_path / "admin.html"
    f.write_text(html, encoding="utf-8")
    ctx = browser.new_context(timezone_id="Asia/Tashkent")
    page = ctx.new_page()
    page.clock.set_fixed_time(TODAY + "T10:00:00+05:00")
    page.route(URL + "/**", fake.handle)
    page.on("pageerror", lambda e: pytest.fail("JS xato: " + str(e)))
    page.goto(f.as_uri())
    return page


def login(page: Page, creds=ADMIN):
    page.fill("#email", creds[0])
    page.fill("#password", creds[1])
    page.click("#loginBtn")


def test_not_configured_shows_setup(browser, fake, tmp_path):
    page = open_page(browser, fake, tmp_path, configured=False)
    expect(page.locator("#setup")).to_be_visible()
    expect(page.locator("#login")).to_be_hidden()
    assert fake.calls == []


def test_wrong_password_shows_uzbek_error(browser, fake, tmp_path):
    page = open_page(browser, fake, tmp_path)
    login(page, ("admin@maktab.uz", "xato"))
    expect(page.locator("#loginError")).to_have_text("Email yoki parol noto'g'ri.")
    expect(page.locator("#panel")).to_be_hidden()


def test_non_admin_is_refused(browser, fake, tmp_path):
    page = open_page(browser, fake, tmp_path)
    login(page, STRANGER)
    expect(page.locator("#loginError")).to_contain_text("admin emas")
    expect(page.locator("#panel")).to_be_hidden()
    assert page.evaluate("sessionStorage.getItem('skuz.session')") is None


def test_admin_sees_ads_statuses_and_stats(browser, fake, tmp_path):
    a = fake.add(title="Ingliz tili kursi", link_url="https://kurs.uz", priority=5)
    fake.add(title="Kelgusi aksiya", starts_on="2026-11-05")
    fake.add(title="Eski aksiya", ends_on="2026-10-01")
    fake.add(title="Pauzada", is_active=False)
    fake.report[a["id"]] = {"views_total": 1200, "clicks_total": 36, "views_recent": 300, "clicks_recent": 9}
    page = open_page(browser, fake, tmp_path)
    login(page)

    cards = page.locator("#ads article")
    expect(cards).to_have_count(4)
    first = cards.nth(0)
    expect(first.locator("h3")).to_contain_text("Ingliz tili kursi")
    expect(first.locator(".badge")).to_have_text("Faol")
    expect(first.locator(".stats")).to_contain_text("Ko'rishlar: 1 200")
    expect(first.locator(".stats")).to_contain_text("Bosishlar: 36")
    expect(first.locator(".stats")).to_contain_text("3,0%")
    expect(page.locator("#ads article", has_text="Kelgusi aksiya").locator(".badge")).to_have_text("Rejalashtirilgan")
    expect(page.locator("#ads article", has_text="Eski aksiya").locator(".badge")).to_have_text("Muddati o'tgan")
    expect(page.locator("#ads article", has_text="Pauzada").locator(".badge")).to_have_text("O'chirilgan")
    expect(page.locator("#tActive")).to_have_text("1")
    expect(page.locator("#tViews")).to_have_text("1 200")
    expect(page.locator("#who")).to_have_text(ADMIN[0])


def test_create_ad_with_image_uploads_then_saves(browser, fake, tmp_path):
    png = tmp_path / "banner.png"
    png.write_bytes(real_png(720, 300))
    page = open_page(browser, fake, tmp_path)
    login(page)
    expect(page.locator(".empty")).to_be_visible()

    page.click("#add")
    page.fill("#fTitle", "Robototexnika to'garagi")
    page.fill("#fLink", "https://t.me/robot_uz")
    page.set_input_files("#fImage", str(png))
    expect(page.locator("#pTitle")).to_have_text("Robototexnika to'garagi")   # jonli ko'rinish
    expect(page.locator("#pImg")).to_be_visible()
    page.fill("#fStart", "2026-11-01")
    page.fill("#fEnd", "2026-11-30")
    page.fill("#fPriority", "7")
    page.click("#save")

    expect(page.locator("#ads article")).to_have_count(1)
    name, (ctype, data) = next(iter(fake.uploads.items()))
    assert re.fullmatch(r"[0-9a-f-]{36}\.png", name)
    assert ctype == "image/png" and data.startswith(b"\x89PNG")
    ad = fake.ads[0]
    assert ad["title"] == "Robototexnika to'garagi"
    assert ad["image_url"] == f"{URL}/storage/v1/object/public/ad-images/{name}"
    assert (ad["starts_on"], ad["ends_on"], ad["priority"], ad["is_active"]) == ("2026-11-01", "2026-11-30", 7, True)
    upload = next(c for c in fake.calls if c[1].startswith("/storage/"))
    assert upload[2]["authorization"] == "Bearer admin-1"  # rasm admin tokeni bilan yuklanadi


@pytest.mark.parametrize("field,value,message", [
    ("#fTitle", "   ", "Sarlavhani yozing."),
    ("#fLink", "http://kurs.uz", "https://"),
    ("#fLink", "kurs.uz", "Havola noto'g'ri"),
])
def test_validation_blocks_bad_input(browser, fake, tmp_path, field, value, message):
    page = open_page(browser, fake, tmp_path)
    login(page)
    page.click("#add")
    page.fill("#fTitle", "Kurs")
    page.fill(field, value)
    page.click("#save")
    expect(page.locator("#formError")).to_contain_text(message)
    assert not any(c[0] == "POST" and c[1] == "/rest/v1/ads" for c in fake.calls)


def test_validation_dates_and_files(browser, fake, tmp_path):
    big = tmp_path / "katta.jpg"
    big.write_bytes(b"\xff\xd8" + b"\0" * (2 * 1024 * 1024 + 10))
    webp = tmp_path / "rasm.webp"
    webp.write_bytes(b"RIFF0000WEBP")
    page = open_page(browser, fake, tmp_path)
    login(page)
    page.click("#add")
    page.fill("#fTitle", "Kurs")
    page.fill("#fStart", "2026-11-10")
    page.fill("#fEnd", "2026-11-01")
    page.click("#save")
    expect(page.locator("#formError")).to_contain_text("Tugash sanasi")
    page.fill("#fEnd", "")
    page.set_input_files("#fImage", str(big))
    expect(page.locator("#formError")).to_contain_text("2 MB")
    page.set_input_files("#fImage", str(webp))
    page.click("#save")
    expect(page.locator("#formError")).to_contain_text("PNG yoki JPG")
    assert fake.uploads == {}


def test_edit_toggle_and_delete(browser, fake, tmp_path):
    img = f"{URL}/storage/v1/object/public/ad-images/eski.png"
    fake.uploads["eski.png"] = ("image/png", b"x")
    ad = fake.add(title="Eski nom", link_url="https://a.uz", image_url=img)
    page = open_page(browser, fake, tmp_path)
    login(page)

    page.click("text=Tahrirlash")
    expect(page.locator("#fTitle")).to_have_value("Eski nom")
    page.fill("#fTitle", "Yangi nom")
    page.click("#save")
    expect(page.locator("#ads h3")).to_contain_text("Yangi nom")
    assert ad["image_url"] == img  # rasm o'zgartirilmasa — saqlanib qoladi

    page.click("text=O'chirib qo'yish")
    expect(page.locator("#ads .badge")).to_have_text("O'chirilgan")
    assert ad["is_active"] is False

    page.once("dialog", lambda d: d.accept())
    page.click("button.danger")
    expect(page.locator(".empty")).to_be_visible()
    assert fake.ads == [] and "eski.png" not in fake.uploads  # rasm ham o'chirildi


def test_delete_cancelled_keeps_ad(browser, fake, tmp_path):
    fake.add(title="Qolsin")
    page = open_page(browser, fake, tmp_path)
    login(page)
    page.once("dialog", lambda d: d.dismiss())
    page.click("button.danger")
    expect(page.locator("#ads article")).to_have_count(1)
    assert len(fake.ads) == 1


def test_titles_are_text_not_html(browser, fake, tmp_path):
    fake.add(title='<img src=x onerror="window.hacked=1">Kurs')
    page = open_page(browser, fake, tmp_path)
    login(page)
    expect(page.locator("#ads h3")).to_contain_text("<img src=x")
    assert page.evaluate("window.hacked") is None
    assert page.locator("#ads img").count() == 0


def test_expired_token_is_refreshed_transparently(browser, fake, tmp_path):
    fake.add(title="Kurs")
    page = open_page(browser, fake, tmp_path)
    login(page)
    expect(page.locator("#ads article")).to_have_count(1)
    fake.expire_next = True
    with page.expect_request(lambda r: "grant_type=refresh_token" in r.url):
        with page.expect_response(lambda r: r.url.endswith("/rpc/ad_report") and r.status == 200):
            page.click("#refresh")
    expect(page.locator("#ads article")).to_have_count(1)
    expect(page.locator("#panelError")).to_be_empty()
    assert fake.refreshed == 1


def test_logout_clears_session(browser, fake, tmp_path):
    page = open_page(browser, fake, tmp_path)
    login(page)
    expect(page.locator("#panel")).to_be_visible()
    page.click("#logout")
    expect(page.locator("#login")).to_be_visible()
    assert page.evaluate("sessionStorage.getItem('skuz.session')") is None


def test_mobile_width_no_horizontal_scroll(browser, fake, tmp_path):
    fake.add(title="Juda uzun sarlavha " * 5, link_url="https://juda-uzun-domen-nomi.uz/" + "a" * 80)
    page = open_page(browser, fake, tmp_path)
    page.set_viewport_size({"width": 375, "height": 800})
    login(page)
    expect(page.locator("#ads article")).to_have_count(1)
    assert page.evaluate("document.documentElement.scrollWidth <= window.innerWidth")


def test_replacing_or_removing_image_deletes_old_file(browser, fake, tmp_path):
    old = f"{URL}/storage/v1/object/public/ad-images/eski.png"
    fake.uploads["eski.png"] = ("image/png", b"x")
    ad = fake.add(title="Kurs", image_url=old)
    png = tmp_path / "yangi.png"
    png.write_bytes(real_png(720, 300))
    page = open_page(browser, fake, tmp_path)
    login(page)

    # Rasm almashtirildi: yangisi yuklanadi, eskisi omborda qolib ketmaydi.
    page.click("text=Tahrirlash")
    page.set_input_files("#fImage", str(png))
    page.click("#save")
    expect(page.locator("#editor")).not_to_be_visible()
    assert "eski.png" not in fake.uploads
    new_name = next(iter(fake.uploads))
    assert ad["image_url"].endswith("/" + new_name)

    # Rasm olib tashlandi: reklama rasmsiz, fayl o'chirildi.
    page.click("text=Tahrirlash")
    page.click("#removeImage")
    expect(page.locator("#pImg")).to_be_hidden()
    page.click("#save")
    expect(page.locator("#editor")).not_to_be_visible()
    assert ad["image_url"] is None and fake.uploads == {}
