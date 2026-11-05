-- AHC Moldes — banco de pedidos online (Supabase / PostgreSQL)
-- Cole tudo no "SQL Editor" do seu projeto Supabase e clique em RUN. Pode rodar de novo sem problema.
--
-- Segurança:
--  * As tabelas ficam fechadas para o público (RLS ligado e sem políticas).
--  * O formulário web (chave pública) só chama duas funções: ver_pedido e enviar_pedido,
--    e só para o pedido cujo código (UUID do link) ele conhece.
--  * O CorelDRAW usa a chave secreta (service_role) para criar, listar e encerrar pedidos.

create extension if not exists pgcrypto;

create table if not exists public.pedidos (
  id               uuid primary key default gen_random_uuid(),
  referencia       text not null,
  status           text not null default 'aberto',      -- aberto | enviado | encerrado
  cliente_nome     text,
  cliente_telefone text,
  cliente_email    text,
  criado_em        timestamptz not null default now(),
  enviado_em       timestamptz
);

create table if not exists public.pedido_itens (
  id           bigint generated always as identity primary key,
  pedido_id    uuid not null references public.pedidos(id) on delete cascade,
  ordem        int  not null default 0,
  tipo         text,
  nome         text,
  numero       text,
  adicional    text,
  tam_camiseta text,
  tam_calcao   text,
  quantidade   int  not null default 1 check (quantidade between 1 and 999)
);

create index if not exists pedido_itens_pedido_idx on public.pedido_itens(pedido_id, ordem);

alter table public.pedidos      enable row level security;
alter table public.pedido_itens enable row level security;
revoke all on public.pedidos, public.pedido_itens from anon, authenticated;

-- Lê um pedido pelo código do link (para o formulário mostrar e permitir editar)
create or replace function public.ver_pedido(p_id uuid)
returns json
language sql
security definer
set search_path = public
as $$
  select json_build_object(
    'id', p.id, 'referencia', p.referencia, 'status', p.status,
    'cliente_nome', p.cliente_nome, 'cliente_telefone', p.cliente_telefone, 'cliente_email', p.cliente_email,
    'itens', coalesce((
      select json_agg(json_build_object(
               'tipo', i.tipo, 'nome', i.nome, 'numero', i.numero, 'adicional', i.adicional,
               'tam_camiseta', i.tam_camiseta, 'tam_calcao', i.tam_calcao, 'quantidade', i.quantidade)
             order by i.ordem)
      from public.pedido_itens i where i.pedido_id = p.id), '[]'::json))
  from public.pedidos p
  where p.id = p_id;
$$;

-- Grava (substitui) os itens do pedido e marca como enviado
create or replace function public.enviar_pedido(p_id uuid, p_cliente json, p_itens json)
returns json
language plpgsql
security definer
set search_path = public
as $$
declare
  st text;
begin
  select status into st from public.pedidos where id = p_id for update;
  if st is null then raise exception 'Pedido não encontrado.'; end if;
  if st = 'encerrado' then raise exception 'Este pedido já foi encerrado e não aceita alterações.'; end if;
  if coalesce(trim(p_cliente->>'nome'), '') = '' then raise exception 'Informe o nome completo.'; end if;
  if json_array_length(p_itens) = 0 then raise exception 'Adicione pelo menos um item.'; end if;
  if json_array_length(p_itens) > 2000 then raise exception 'Itens demais em um pedido.'; end if;

  delete from public.pedido_itens where pedido_id = p_id;

  insert into public.pedido_itens (pedido_id, ordem, tipo, nome, numero, adicional, tam_camiseta, tam_calcao, quantidade)
  select p_id, t.ord,
         left(trim(t.x->>'tipo'), 40),
         left(trim(t.x->>'nome'), 60),
         left(trim(t.x->>'numero'), 10),
         left(trim(t.x->>'adicional'), 60),
         upper(left(replace(trim(t.x->>'tam_camiseta'), ' ', ''), 10)),
         upper(left(replace(trim(t.x->>'tam_calcao'), ' ', ''), 10)),
         greatest(1, least(999, coalesce(nullif(regexp_replace(t.x->>'quantidade', '[^0-9]', '', 'g'), '')::int, 1)))
  from json_array_elements(p_itens) with ordinality as t(x, ord);

  update public.pedidos
     set status = 'enviado',
         enviado_em = now(),
         cliente_nome = left(trim(p_cliente->>'nome'), 100),
         cliente_telefone = left(trim(p_cliente->>'telefone'), 30),
         cliente_email = left(trim(p_cliente->>'email'), 100)
   where id = p_id;

  return json_build_object('ok', true);
end;
$$;

revoke all on function public.ver_pedido(uuid) from public;
revoke all on function public.enviar_pedido(uuid, json, json) from public;
grant execute on function public.ver_pedido(uuid) to anon, authenticated;
grant execute on function public.enviar_pedido(uuid, json, json) to anon, authenticated;
