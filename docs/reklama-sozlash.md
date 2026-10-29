# Reklama tizimini sozlash (Supabase + admin sahifa)

Bir martalik sozlash taxminan 20 daqiqa oladi. Hammasi bepul: Supabase'ning bepul tarifi yetadi.

## 1. Supabase loyihasini ochish

1. https://supabase.com saytiga kiring → **Start your project** → GitHub yoki email bilan ro'yxatdan o'ting.
2. **New project** tugmasini bosing:
   - **Name:** `skrinshotuz-reklama` (istalgan nom);
   - **Database Password:** kuchli parol kiriting va saqlab qo'ying (keyin kamdan-kam kerak bo'ladi);
   - **Region:** sizga eng yaqini (masalan, *Central EU (Frankfurt)*).
3. **Create new project** ni bosing va 1–2 daqiqa kuting.

## 2. Jadvallarni yaratish

1. Chap menyudan **SQL Editor** → **New query** ni oching.
2. Repozitoriydagi [`server/supabase.sql`](../server/supabase.sql) faylining **hammasini** nusxalab qo'ying va **Run** ni bosing.
3. Natijada "Success. No rows returned" chiqishi kerak.

Bu skript quyidagilarni yaratadi:
- reklamalar jadvali;
- kunlik statistika jadvali — faqat umumiy sonlar;
- rasmlar uchun `ad-images` saqlash joyi;
- kim nima qila olishini belgilaydigan huquqlar.

Skriptni qayta ishga tushirish xavfsiz: mavjud ma'lumot o'chmaydi.

## 3. Admin hisobini yaratish

1. **Authentication → Users → Add user → Create new user**:
   - email va parolingizni kiriting;
   - **Auto Confirm User** belgisini qo'ying → **Create user**.
2. **SQL Editor**'da quyidagini emailingiz bilan ishga tushiring:
   ```sql
   insert into public.admins (user_id)
   select id from auth.users where email = 'sizning@email.uz'
   on conflict do nothing;
   ```
3. **Begonalar ro'yxatdan o'tmasin:** **Authentication → Sign In / Providers** ga kiring va **Allow new users to sign up** ni o'chiring.
   - O'chirilmasa ham, begona odam reklamaga tega olmaydi (buni testlar tekshiradi). Lekin o'chirib qo'ygan yaxshi.

## 4. Kalitlarni olish

**Project Settings → API Keys** (yoki **Data API**) bo'limidan ikkita qiymatni oling:
- **Project URL** — masalan, `https://abcdefgh.supabase.co`;
- **Publishable key** (`sb_publishable_...`) yoki eski **anon public** kalit (`eyJ...`).

> ⚠️ **Secret** / **service_role** kalitni hech qayerga — na admin sahifaga, na GitHub'ga, na menga — bermang. U bazaga to'liq ruxsat beradi.
> Dastur va admin sahifa faqat ommaviy kalit bilan ishlaydi. Dastur maxfiy kalitni bilib turib rad etadi.

## 5. Admin sahifani saytingizga qo'yish

1. [`admin/reklama-admin.html`](../admin/reklama-admin.html) faylini yuklab oling.
2. Uni Bloknot yoki istalgan muharrirda oching va quyidagi ikki qatorni o'zgartiring:
   ```js
   const SUPABASE_URL = "https://abcdefgh.supabase.co";
   const SUPABASE_KEY = "sb_publishable_...";
   ```
3. Faylni saytingizga yuklang. Nomini o'zgartirish mumkin, masalan `sayt.uz/skuz-panel.html`.
   - Sayt **https** bilan ochilishi kerak.
4. Sahifani oching va 3-qadamdagi email va parol bilan kiring.

Sahifa ochiq turgani xavfli emas: login qilmagan yoki admin bo'lmagan odam hech narsa ko'rmaydi va o'zgartira olmaydi.

## 6. Dasturga Supabase manzilini berish (GitHub)

1. GitHub'da repozitoriyni oching → **Settings → Secrets and variables → Actions** → **Variables** yorlig'i → **New repository variable**.
2. Ikkita o'zgaruvchi qo'shing:
   - `ADS_SUPABASE_URL` = Project URL;
   - `ADS_SUPABASE_KEY` = Publishable (yoki anon) kalit.
3. Yangi versiya chiqaring: **Releases → Draft a new release** → teg `v1.1.0` → **Publish release**.
   - 10–15 daqiqada fayllar tayyor bo'ladi.
   - Bu o'zgaruvchilarsiz yig'ilgan versiyada reklama chiqmaydi, CI buni ogohlantiradi.

## 7. Birinchi reklama va tekshirish

1. **Admin sahifada reklama qo'shing:** **+ Yangi reklama** → sarlavha, havola (`https://...`), rasm (PNG/JPG, 2 MB gacha, tavsiya 720×300) → **Saqlash**.
   - O'ng tomonda reklama dasturda qanday ko'rinishi ko'rsatiladi.
2. **Kompyuterda tekshiring:**
   - yangi `SkrinshotUZ_Setup.exe` ni o'rnating va dasturni oching;
   - taxminan 20 soniyadan keyin ekranning pastki o'ng burchagida reklama chiqadi;
   - "Batafsil" ni bosing.
3. **Admin sahifada "Yangilash" ni bosing:** ko'rishlar va bosishlar soni oshgan bo'lishi kerak.

**Shu kompyuterda bugun qayta sinash uchun:**
1. Dasturdan chiqing.
2. `%APPDATA%\Skrinshot UZ\settings.json` faylidan `"AdsLastShownOn": "…"` qatorini o'chiring.
3. Dasturni qayta oching.

## Qanday ishlaydi (qisqacha)

- **Qachon chiqadi:** dastur ishga tushgandan 20 soniya keyin, keyin har soatda tekshiradi. Reklama **kuniga ko'pi bilan bir marta** chiqadi.
  - Skrinshot yoki chop etish oynasi ochiq bo'lsa, kutadi.
  - Internet yo'q bo'lsa, jim o'tib ketadi.
- **Qaysi reklama chiqadi:** bir nechta faol reklama bo'lsa, har kuni navbatdagisi chiqadi (ustuvorligi yuqorisidan boshlab).
- **Faol reklama:** "Faol" belgisi yoqilgan va bugun boshlanish–tugash sanalari ichida bo'lgan reklama. Sanalar Toshkent vaqti bo'yicha.
- **Statistika:** har bir reklama uchun kunlik "ko'rildi" va "bosildi" soni. Kim ko'rgani saqlanmaydi.
- **Bepul tarif:** Supabase bepul loyihani uzoq vaqt faoliyat bo'lmasa to'xtatib qo'yishi mumkin.
  - Bunda reklama shunchaki chiqmaydi, dastur ishlayveradi.
  - Supabase panelida **Restore** tugmasi bilan qayta yoqiladi.
  - Dasturlar har kuni murojaat qilgani uchun odatda bunday bo'lmaydi.

## Muhim eslatma

Dasturdan asosan maktablarda bolalar foydalanadi. Reklama mazmuni bolalar uchun mos bo'lishi va O'zbekistonning reklama to'g'risidagi qonunchiligiga (voyaga yetmaganlarga reklama talablari) mos kelishi kerak. Reklama tarqatishdan oldin huquqshunos bilan maslahatlashing.
