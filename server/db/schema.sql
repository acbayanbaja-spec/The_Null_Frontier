-- Null Frontier online services schema for Supabase (PostgreSQL)
-- Run this in the Supabase SQL Editor. Assumes the project uses Supabase Auth.

-- ---------------------------------------------------------------------------
-- Profiles: one row per auth user, created automatically by the trigger below.
-- ---------------------------------------------------------------------------
create table if not exists public.profiles (
  id uuid primary key references auth.users (id) on delete cascade,
  username text not null,
  created_at timestamptz not null default now(),
  constraint profiles_username_unique unique (username)
);

alter table public.profiles enable row level security;

-- Profiles are world-readable (public username for leaderboards).
-- Only insert/update of your own row is allowed.
create policy "profiles_select_all"
  on public.profiles for select
  using (true);

create policy "profiles_insert_own"
  on public.profiles for insert
  with check (auth.uid() = id);

create policy "profiles_update_own"
  on public.profiles for update
  using (auth.uid() = id)
  with check (auth.uid() = id);

-- ---------------------------------------------------------------------------
-- Player saves: one save blob per user (upserted by the client).
-- ---------------------------------------------------------------------------
create table if not exists public.player_saves (
  user_id uuid primary key references auth.users (id) on delete cascade,
  save_data jsonb not null default '{}'::jsonb,
  updated_at timestamptz not null default now()
);

alter table public.player_saves enable row level security;

create policy "saves_select_own"
  on public.player_saves for select
  using (auth.uid() = user_id);

create policy "saves_insert_own"
  on public.player_saves for insert
  with check (auth.uid() = user_id);

create policy "saves_update_own"
  on public.player_saves for update
  using (auth.uid() = user_id)
  with check (auth.uid() = user_id);

-- ---------------------------------------------------------------------------
-- Leaderboards: one row per (leaderboard, user). World-readable, own-writable.
-- ---------------------------------------------------------------------------
create table if not exists public.leaderboard_entries (
  leaderboard_id text not null,
  user_id uuid not null references auth.users (id) on delete cascade,
  score bigint not null default 0,
  updated_at timestamptz not null default now(),
  primary key (leaderboard_id, user_id)
);

create index if not exists leaderboard_entries_score_idx
  on public.leaderboard_entries (leaderboard_id, score desc);

alter table public.leaderboard_entries enable row level security;

create policy "leaderboard_select_all"
  on public.leaderboard_entries for select
  using (true);

create policy "leaderboard_insert_own"
  on public.leaderboard_entries for insert
  with check (auth.uid() = user_id);

create policy "leaderboard_update_own"
  on public.leaderboard_entries for update
  using (auth.uid() = user_id)
  with check (auth.uid() = user_id);

-- ---------------------------------------------------------------------------
-- Friends: symmetric requests, status pending -> accepted.
-- ---------------------------------------------------------------------------
create table if not exists public.friends (
  user_id uuid not null references auth.users (id) on delete cascade,
  friend_id uuid not null references auth.users (id) on delete cascade,
  status text not null default 'pending' check (status in ('pending', 'accepted')),
  created_at timestamptz not null default now(),
  primary key (user_id, friend_id),
  constraint friends_no_self check (user_id <> friend_id)
);

alter table public.friends enable row level security;

create policy "friends_select_own"
  on public.friends for select
  using (auth.uid() = user_id or auth.uid() = friend_id);

create policy "friends_insert_own"
  on public.friends for insert
  with check (auth.uid() = user_id);

create policy "friends_update_own"
  on public.friends for update
  using (auth.uid() = user_id or auth.uid() = friend_id)
  with check (auth.uid() = user_id or auth.uid() = friend_id);

create policy "friends_delete_own"
  on public.friends for delete
  using (auth.uid() = user_id or auth.uid() = friend_id);

-- ---------------------------------------------------------------------------
-- Achievements: one row per unlocked achievement.
-- ---------------------------------------------------------------------------
create table if not exists public.achievements (
  user_id uuid not null references auth.users (id) on delete cascade,
  achievement_id text not null,
  unlocked_at timestamptz not null default now(),
  primary key (user_id, achievement_id)
);

alter table public.achievements enable row level security;

create policy "achievements_select_own"
  on public.achievements for select
  using (auth.uid() = user_id);

create policy "achievements_insert_own"
  on public.achievements for insert
  with check (auth.uid() = user_id);

-- ---------------------------------------------------------------------------
-- Trigger: create a profile when a new auth user signs up.
-- Username comes from signUp metadata (data: { username }).
-- ---------------------------------------------------------------------------
create or replace function public.handle_new_user()
returns trigger
language plpgsql
security definer set search_path = public
as $$
begin
  insert into public.profiles (id, username)
  values (new.id, coalesce(new.raw_user_meta_data ->> 'username', 'player-' || substr(new.id::text, 1, 8)))
  on conflict (id) do nothing;
  return new;
end;
$$;

drop trigger if exists on_auth_user_created on auth.users;
create trigger on_auth_user_created
  after insert on auth.users
  for each row execute function public.handle_new_user();
