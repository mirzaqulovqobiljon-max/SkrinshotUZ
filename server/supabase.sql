-- =====================================================================================
--  Skrinshot UZ — reklama tizimi: Supabase sxemasi
--
--  Qayerda ishga tushiriladi: Supabase → SQL Editor → shu faylning hammasini qo'yib → Run.
--  Qayta ishga tushirish xavfsiz (mavjud ma'lumot o'chmaydi).
--
--  Maxfiylik qoidasi (bolalar ma'lumoti): dastur serverga HECH QANDAY shaxsiy ma'lumot yubormaydi.
--  Statistika faqat umumiy sonlar: "shu reklama shu kuni N marta ko'rildi / bosildi".
--  Kompyuter, foydalanuvchi, IP manzil yoki skrinshot bu bazada saqlanmaydi.
--
--  Kim nima qila oladi:
--    * dastur (anon kalit)    — faqat faol reklamalarni o'qiydi (active_ads) va hisoblagichni oshiradi (record_ad_event);
--    * admin (login bilan)    — reklamalarni qo'shadi/o'zgartiradi/o'chiradi, rasm yuklaydi, statistikani ko'radi;
--    * boshqa hech kim        — hech narsa (jadvallar to'g'ridan-to'g'ri ochiq emas).
-- =====================================================================================

-- ---------- Adminlar ----------
create table if not exists public.admins (
  user_id    uuid primary key references auth.users(id) on delete cascade,
  created_at timestamptz not null default now()
);
alter table public.admins enable row level security;
-- Siyosat yo'q: adminlar ro'yxatini faqat SQL Editor orqali o'zgartirish mumkin.

create or replace function public.is_admin()
returns boolean
language sql stable security definer
set search_path = public
as $$
  select exists (select 1 from public.admins where user_id = auth.uid());
$$;
revoke all on function public.is_admin() from public;
grant execute on function public.is_admin() to anon, authenticated;

-- ---------- Reklamalar ----------
create table if not exists public.ads (
  id         uuid primary key default gen_random_uuid(),
  title      text not null check (char_length(btrim(title)) between 1 and 120),
  image_url  text check (image_url is null or image_url ~ '^https://[^\s]+$'),
  link_url   text check (link_url is null or link_url ~ '^https://[^\s]+$'),
  starts_on  date,
  ends_on    date,
  priority   integer not null default 0 check (priority between -100 and 100),
  is_active  boolean not null default true,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  constraint ads_dates_ok check (starts_on is null or ends_on is null or ends_on >= starts_on)
);
alter table public.ads enable row level security;

drop policy if exists ads_admin_all on public.ads;
create policy ads_admin_all on public.ads
  for all to authenticated
  using (public.is_admin())
  with check (public.is_admin());

create or replace function public.ads_touch_updated_at()
returns trigger language plpgsql as $$
begin
  new.updated_at := now();
  return new;
end $$;
drop trigger if exists ads_touch_updated_at on public.ads;
create trigger ads_touch_updated_at before update on public.ads
  for each row execute function public.ads_touch_updated_at();

-- ---------- Statistika (faqat kunlik umumiy sonlar) ----------
create table if not exists public.ad_stats (
  ad_id  uuid    not null references public.ads(id) on delete cascade,
  day    date    not null,
  views  integer not null default 0 check (views >= 0),
  clicks integer not null default 0 check (clicks >= 0),
  primary key (ad_id, day)
);
alter table public.ad_stats enable row level security;

drop policy if exists ad_stats_admin_read on public.ad_stats;
create policy ad_stats_admin_read on public.ad_stats
  for select to authenticated
  using (public.is_admin());

-- Toshkent vaqti bo'yicha bugungi sana (sanalar admin panelda shu vaqtda kiritiladi).
create or replace function public.tashkent_today()
returns date language sql stable
as $$ select (now() at time zone 'Asia/Tashkent')::date; $$;

-- ---------- Dastur uchun: faol reklamalar ----------
create or replace function public.active_ads()
returns table (id uuid, title text, image_url text, link_url text, priority integer)
language sql stable security definer
set search_path = public
as $$
  select a.id, a.title, a.image_url, a.link_url, a.priority
  from public.ads a
  where a.is_active
    and (a.starts_on is null or a.starts_on <= public.tashkent_today())
    and (a.ends_on   is null or a.ends_on   >= public.tashkent_today())
  order by a.priority desc, a.created_at desc
  limit 20;
$$;
revoke all on function public.active_ads() from public;
grant execute on function public.active_ads() to anon, authenticated;

-- ---------- Dastur uchun: "ko'rildi" / "bosildi" hisoblagichi ----------
create or replace function public.record_ad_event(p_ad_id uuid, p_kind text)
returns void
language plpgsql volatile security definer
set search_path = public
as $$
begin
  if p_kind is null or p_kind not in ('view', 'click') then
    raise exception 'Noto''g''ri hodisa turi: %', p_kind using errcode = '22023';
  end if;

  -- O'chirilgan yoki faol bo'lmagan reklama uchun jim e'tiborsiz qoldiriladi.
  if not exists (select 1 from public.ads where id = p_ad_id and is_active) then
    return;
  end if;

  insert into public.ad_stats as s (ad_id, day, views, clicks)
  values (p_ad_id, public.tashkent_today(), (p_kind = 'view')::int, (p_kind = 'click')::int)
  on conflict (ad_id, day) do update
    set views  = s.views  + excluded.views,
        clicks = s.clicks + excluded.clicks;
end $$;
revoke all on function public.record_ad_event(uuid, text) from public;
grant execute on function public.record_ad_event(uuid, text) to anon, authenticated;

-- ---------- Admin panel uchun: hisobot ----------
create or replace function public.ad_report(p_days integer default 7)
returns table (ad_id uuid, views_total bigint, clicks_total bigint, views_recent bigint, clicks_recent bigint)
language sql stable security definer
set search_path = public
as $$
  select s.ad_id,
         sum(s.views)::bigint,
         sum(s.clicks)::bigint,
         coalesce(sum(s.views)  filter (where s.day > public.tashkent_today() - greatest(p_days, 1)), 0)::bigint,
         coalesce(sum(s.clicks) filter (where s.day > public.tashkent_today() - greatest(p_days, 1)), 0)::bigint
  from public.ad_stats s
  where public.is_admin()
  group by s.ad_id;
$$;
-- Supabase yangi funksiyalarga anon'ga ham avtomatik huquq beradi — shuning uchun aniq olib tashlanadi.
revoke all on function public.ad_report(integer) from public, anon;
grant execute on function public.ad_report(integer) to authenticated;

-- ---------- Rasmlar (Storage) ----------
-- Ommaviy o'qiladi (dastur rasmni yuklab oladi), yozish faqat adminga. Dastur faqat PNG/JPG ni ko'rsata oladi.
insert into storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
values ('ad-images', 'ad-images', true, 2097152, array['image/png', 'image/jpeg'])
on conflict (id) do update
  set public = true,
      file_size_limit = excluded.file_size_limit,
      allowed_mime_types = excluded.allowed_mime_types;

drop policy if exists ad_images_admin_insert on storage.objects;
create policy ad_images_admin_insert on storage.objects
  for insert to authenticated
  with check (bucket_id = 'ad-images' and public.is_admin());

drop policy if exists ad_images_admin_update on storage.objects;
create policy ad_images_admin_update on storage.objects
  for update to authenticated
  using (bucket_id = 'ad-images' and public.is_admin())
  with check (bucket_id = 'ad-images' and public.is_admin());

drop policy if exists ad_images_admin_delete on storage.objects;
create policy ad_images_admin_delete on storage.objects
  for delete to authenticated
  using (bucket_id = 'ad-images' and public.is_admin());

-- ---------- Birinchi adminni qo'shish ----------
-- 1) Authentication → Users → "Add user" (email + parol, "Auto Confirm User" belgilang).
-- 2) Quyidagi qatordagi emailni o'zingiznikiga almashtirib, alohida ishga tushiring:
--
--    insert into public.admins (user_id)
--    select id from auth.users where email = 'sizning@email.uz'
--    on conflict do nothing;
