# Pedidos online — configurar (uma vez só, ~10 minutos)

## 1. Criar o banco (Supabase, grátis)
1. Acesse https://supabase.com → **Start your project** → entre com sua conta Google/GitHub.
2. **New project**: nome `ahc-pedidos`, crie uma senha do banco (guarde), região **South America (São Paulo)**.
3. Quando terminar de criar, abra **SQL Editor** (menu da esquerda) → **New query**.
4. Abra o arquivo `supabase.sql` (nesta pasta), copie tudo, cole lá e clique em **Run**. Deve aparecer "Success".
5. Vá em **Project Settings → API** (ou **API Keys**) e anote:
   - **Project URL** (ex.: `https://abcd1234.supabase.co`)
   - Chave **pública**: `anon` / `publishable` (começa com `eyJ...` ou `sb_publishable_...`)
   - Chave **secreta**: `service_role` / `secret` (começa com `eyJ...` ou `sb_secret_...`) — **não compartilhe**

## 2. Publicar o formulário (Netlify, grátis)
1. Abra `config.js` nesta pasta (Bloco de Notas) e troque:
   - `https://SEU-PROJETO.supabase.co` → seu **Project URL**
   - `SUA-CHAVE-PUBLICA` → a chave **pública** (nunca a secreta!)
   - Se quiser, ajuste a lista de tamanhos e tipos.
2. Acesse https://app.netlify.com/drop, crie a conta e **arraste a pasta `web`** para a página.
3. O Netlify mostra o endereço do site (ex.: `https://ahc-pedidos.netlify.app`). Em *Site configuration → Change site name* dá para escolher um nome melhor.

## 3. Ligar o CorelDRAW
Na barra **AHC Moldes → Configurações → Pedidos online**:
- **Endereço do projeto**: o Project URL
- **Chave SECRETA**: a service_role / sb_secret
- **Endereço do formulário**: o endereço do Netlify (ex.: `https://ahc-pedidos.netlify.app/`)

Clique em **SALVAR CONFIGURAÇÕES**.

## Uso no dia a dia
1. **Montar pedido → ☁ Pedidos online → + Novo pedido** (digite a referência, ex.: "Atlético Estância").
   O link já vai para a área de transferência: mande para o cliente (WhatsApp, e-mail).
2. O cliente abre o link, preenche ou importa a planilha e clica **Enviar pedido**.
   Ele pode voltar no mesmo link e corrigir enquanto o pedido estiver aberto.
3. No Corel: **☁ Pedidos online** → escolha o pedido → **Usar este pedido** → confira → **MONTAR PEDIDO**.
4. Quando terminar a produção: **Encerrar pedido** (o cliente não consegue mais alterar).
