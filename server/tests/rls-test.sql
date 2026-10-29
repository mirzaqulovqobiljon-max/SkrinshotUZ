-- Sxema testlari: kim nimani qila oladi (RLS), sanalar, hisoblagich, cheklovlar.
-- Ishga tushirish: psql -v ON_ERROR_STOP=1 -f supabase-stub.sql -f ../supabase.sql -f ../supabase.sql -f rls-test.sql
\set QUIET on
create schema if not exists t;
grant usage on schema t to anon, authenticated;

create or replace function t.check(ok boolean, what text) returns void language plpgsql as $$
begin
  if ok is distinct from true then raise exception 'TEST YIQILDI: %', what; end if;
  raise notice 'o''tdi: %', what;
end $$;

-- sql bajarilganda xato bo'lishi kerak (kutilgan SQLSTATE bilan yoki istalgan xato).
create or replace function t.fails(sql text, what text, state text default null) returns void language plpgsql as $$
begin
  execute sql;
  raise exception 'TEST YIQILDI (xato kutilgan edi): %', what;
exception when others then
  if sqlerrm like 'TEST YIQILDI%' then raise; end if;
  if state is not null and sqlstate <> state then
    raise exception 'TEST YIQILDI: % — kutilgan %, olingan % (%)', what, state, sqlstate, sqlerrm;
  end if;
  raise notice 'o''tdi: % (%)', what, sqlstate;
end $$;
-- sql nechta qatorga ta'sir qilganini qaytaradi (RLS jim "0 qator" qiladi — xato bermaydi).
create or replace function t.affected(sql text) returns bigint language plpgsql as $$
declare n bigint;
begin
  execute sql;
  get diagnostics n = row_count;
  return n;
end $$;
grant execute on all functions in schema t to anon, authenticated;

-- ---------- Tayyorgarlik (postgres = SQL Editor huquqi) ----------
insert into auth.users values
  ('11111111-1111-1111-1111-111111111111', 'admin@test.uz'),
  ('22222222-2222-2222-2222-222222222222', 'begona@test.uz');
insert into public.admins (user_id) select id from auth.users where email = 'admin@test.uz';
insert into storage.buckets (id, name, public) values ('boshqa-bucket', 'boshqa-bucket', true) on conflict do nothing;

-- Sana Toshkent vaqti bo'yicha bo'lishi: seans vaqt zonasini Toshkent sanasidan farq qiladigan qilib tanlaymiz
-- (aks holda kunning ko'p qismida current_date bilan farqi sezilmaydi).
do $$
declare tashkent timestamp := now() at time zone 'Asia/Tashkent';
begin
  if extract(hour from tashkent) < 17 then
    set timezone = 'Etc/GMT+12';   -- UTC-12: mahalliy sana Toshkentdan bir kun oldin
  else
    set timezone = 'Etc/GMT-14';   -- UTC+14: mahalliy sana Toshkentdan bir kun keyin
  end if;
  perform t.check(current_date <> tashkent::date, 'test sharti: seans sanasi Toshkentnikidan farq qiladi');
  perform t.check(public.tashkent_today() = tashkent::date, 'tashkent_today() — server/seans vaqt zonasidan qat''i nazar Toshkent sanasi');
  reset timezone;
end $$;

insert into public.ads (id, title, image_url, link_url, starts_on, ends_on, priority, is_active) values
  ('aaaaaaaa-0000-0000-0000-000000000001', 'Faol, muddatsiz', 'https://x.supabase.co/a.png', 'https://kurs.uz', null, null, 0, true),
  ('aaaaaaaa-0000-0000-0000-000000000002', 'Faol, bugun tugaydi', null, 'https://t.me/kanal', public.tashkent_today() - 3, public.tashkent_today(), 5, true),
  ('aaaaaaaa-0000-0000-0000-000000000003', 'Ertaga boshlanadi', null, null, public.tashkent_today() + 1, null, 9, true),
  ('aaaaaaaa-0000-0000-0000-000000000004', 'Kecha tugagan', null, null, null, public.tashkent_today() - 1, 9, true),
  ('aaaaaaaa-0000-0000-0000-000000000005', 'O''chirilgan (nofaol)', null, null, null, null, 9, false);

-- ---------- Dastur (anon) ----------
set role anon;
select t.check((select count(*) from public.ads) = 0, 'anon ads jadvalini to''g''ridan-to''g''ri ko''rmaydi');
select t.check((select count(*) from public.ad_stats) = 0, 'anon statistikani ko''rmaydi');
select t.check((select count(*) from public.admins) = 0, 'anon adminlar ro''yxatini ko''rmaydi');
select t.fails($$insert into public.ads (title) values ('buzg''unchi')$$, 'anon reklama qo''sha olmaydi', '42501');
select t.check(t.affected($$update public.ads set is_active = false$$) = 0, 'anon reklamani o''chirib qo''ya olmaydi');
select t.check(t.affected($$delete from public.ads$$) = 0, 'anon reklamani o''chira olmaydi');
select t.fails($$insert into public.ad_stats (ad_id, day, views) values ('aaaaaaaa-0000-0000-0000-000000000001', current_date, 1000)$$, 'anon statistikani qo''lda yoza olmaydi', '42501');
select t.fails($$select * from public.ad_report(7)$$, 'anon hisobotni ololmaydi', '42501');
select t.fails($$insert into storage.objects (bucket_id, name) values ('ad-images', 'x.png')$$, 'anon rasm yuklay olmaydi', '42501');

select t.check(
  (select array_agg(title order by priority desc) from public.active_ads()) = array['Faol, bugun tugaydi', 'Faol, muddatsiz'],
  'active_ads: faqat faol va muddati ichidagilar, ustuvorlik bo''yicha');
select t.check((select count(*) from public.active_ads() where title = 'Ertaga boshlanadi') = 0, 'hali boshlanmagan reklama chiqmaydi');
select t.check((select count(*) from public.active_ads() where title = 'Kecha tugagan') = 0, 'muddati o''tgan reklama chiqmaydi');

select public.record_ad_event('aaaaaaaa-0000-0000-0000-000000000001', 'view');
select public.record_ad_event('aaaaaaaa-0000-0000-0000-000000000001', 'view');
select public.record_ad_event('aaaaaaaa-0000-0000-0000-000000000001', 'click');
select public.record_ad_event('aaaaaaaa-0000-0000-0000-000000000005', 'view');  -- nofaol: e'tiborsiz
select public.record_ad_event('bbbbbbbb-0000-0000-0000-000000000000', 'view');  -- yo'q reklama: e'tiborsiz
select t.fails($$select public.record_ad_event('aaaaaaaa-0000-0000-0000-000000000001', 'hack')$$, 'noto''g''ri hodisa turi rad etiladi', '22023');
select t.fails($$select public.record_ad_event('aaaaaaaa-0000-0000-0000-000000000001', null)$$, 'bo''sh hodisa turi rad etiladi', '22023');
reset role;

select t.check((select views from public.ad_stats where ad_id = 'aaaaaaaa-0000-0000-0000-000000000001') = 2, 'ko''rishlar soni = 2');
select t.check((select clicks from public.ad_stats where ad_id = 'aaaaaaaa-0000-0000-0000-000000000001') = 1, 'bosishlar soni = 1');
select t.check((select count(*) from public.ad_stats) = 1, 'nofaol/yo''q reklama uchun statistika yozilmadi');
select t.check((select day from public.ad_stats limit 1) = public.tashkent_today(), 'kun Toshkent vaqti bo''yicha');

-- ---------- Ro'yxatdan o'tgan, lekin admin bo'lmagan foydalanuvchi ----------
set role authenticated;
set request.jwt.claim.sub = '22222222-2222-2222-2222-222222222222';
select t.check(not public.is_admin(), 'begona foydalanuvchi admin emas');
select t.check((select count(*) from public.ads) = 0, 'begona reklamalarni ko''rmaydi');
select t.check((select count(*) from public.ad_stats) = 0, 'begona statistikani ko''rmaydi');
select t.check((select count(*) from public.ad_report(7)) = 0, 'begona hisobot ololmaydi');
select t.fails($$insert into public.ads (title) values ('begona')$$, 'begona reklama qo''sha olmaydi', '42501');
select t.check(t.affected($$update public.ads set title = 'buzildi'$$) = 0, 'begona reklamani o''zgartira olmaydi');
select t.check(t.affected($$delete from public.ads$$) = 0, 'begona reklamani o''chira olmaydi');
select t.check(t.affected($$delete from public.ad_stats$$) = 0, 'begona statistikani o''chira olmaydi');
select t.fails($$insert into public.admins (user_id) values ('22222222-2222-2222-2222-222222222222')$$, 'begona o''zini admin qila olmaydi', '42501');
select t.fails($$insert into storage.objects (bucket_id, name) values ('ad-images', 'x.png')$$, 'begona rasm yuklay olmaydi', '42501');
reset role;

-- ---------- Admin ----------
set role authenticated;
set request.jwt.claim.sub = '11111111-1111-1111-1111-111111111111';
select t.check(public.is_admin(), 'admin taniladi');
select t.check((select count(*) from public.ads) = 5, 'admin barcha reklamalarni ko''radi');
insert into public.ads (title, link_url) values ('Admin qo''shdi', 'https://sayt.uz/kurs');
update public.ads set priority = 3 where title = 'Admin qo''shdi';
select t.check((select updated_at >= created_at from public.ads where title = 'Admin qo''shdi'), 'updated_at yangilanadi');
insert into storage.objects (bucket_id, name) values ('ad-images', 'banner.png');
select t.fails($$insert into storage.objects (bucket_id, name) values ('boshqa-bucket', 'x.png')$$, 'admin ham boshqa bucket''ga yozolmaydi (faqat ad-images)');
select t.check(
  (select views_total = 2 and clicks_total = 1 and views_recent = 2 from public.ad_report(7) where ad_id = 'aaaaaaaa-0000-0000-0000-000000000001'),
  'hisobot: jami va oxirgi 7 kun');

-- Cheklovlar
select t.fails($$insert into public.ads (title, link_url) values ('http', 'http://sayt.uz')$$, 'havola faqat https', '23514');
select t.fails($$insert into public.ads (title, image_url) values ('rasm', 'ftp://x/y.png')$$, 'rasm manzili faqat https', '23514');
select t.fails($$insert into public.ads (title) values ('   ')$$, 'bo''sh sarlavha rad etiladi', '23514');
select t.fails($$insert into public.ads (title, starts_on, ends_on) values ('sana', '2026-11-10', '2026-11-01')$$, 'tugash sanasi boshlanishdan oldin bo''lmaydi', '23514');
select t.fails($$insert into public.ads (title, priority) values ('ustuvor', 1000)$$, 'ustuvorlik chegarada', '23514');

-- O'chirish statistikani ham o'chiradi
delete from public.ads where id = 'aaaaaaaa-0000-0000-0000-000000000001';
reset role;
select t.check((select count(*) from public.ad_stats) = 0, 'reklama o''chirilsa statistikasi ham o''chadi');

-- Storage sozlamalari
select t.check((select public and file_size_limit = 2097152 and allowed_mime_types = array['image/png','image/jpeg'] from storage.buckets where id = 'ad-images'),
  'ad-images: ommaviy, 2 MB, faqat PNG/JPG');

\echo 'BARCHA SXEMA TESTLARI O''TDI'
