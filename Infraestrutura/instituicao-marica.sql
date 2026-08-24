-- =====================================================================================
-- Semeia a identidade da instituição de MARICÁ (ADR-0043).
--
-- QUANDO RODAR: uma vez, no deploy que aplicar a migration `AddInstituicao`, DEPOIS de
-- confirmar que a tabela existe (`smsmarica.__migrations`) e ANTES de considerar o deploy
-- concluído.
--
-- POR QUE ISTO EXISTE: sem esta linha a tabela nasce vazia e o código cai no padrão neutro
-- — que é deliberadamente SEM nome de município. A página pública de verificação da
-- Declaração de Comparecimento (o QR impresso, já em circulação) passaria a dizer só
-- "Secretaria Municipal de Saúde", e o título da aba do painel viraria "Saúde — Painel".
--
-- POR QUE NÃO É UMA MIGRATION: migration é imutável e roda igual em toda instância nova —
-- semear Maricá nela plantaria o nome errado no banco de outro município (ADR-0043, regra 3).
--
-- IDEMPOTENTE: `ON CONFLICT DO NOTHING`. Rodar de novo não faz nada e, principalmente, NÃO
-- sobrescreve o que alguém já tiver ajustado pela tela Sistema → Instituição. Para corrigir
-- um valor depois, use a tela — não reescreva este arquivo.
-- =====================================================================================

INSERT INTO smsmarica.instituicao (
    id,
    nome,
    nome_secretaria,
    nome_curto,
    sigla,
    cnpj,
    codigo_ibge,
    uf,
    ddd_padrao,
    telefone,
    email_contato,
    email_dpo,
    whatsapp_numero_publico,
    logo_midia_id,
    favicon_midia_id,
    cor_primaria,
    cor_secundaria,
    cor_gradiente_inicio,
    cor_gradiente_fim,
    url_painel,
    url_app,
    url_arquivos,
    assinatura_produto_html,
    atualizado_por_usuario_id,
    atualizado_em
) VALUES (
    '00000000-0000-0000-0000-00000000fffe',   -- PK fixa do singleton (Instituicao.IdSingleton)

    'Prefeitura Municipal de Maricá',
    'Secretaria Municipal de Saúde de Maricá', -- encabeça laudos, declarações e páginas públicas
    'Saúde Maricá',                            -- ver NOTA 1
    'SMS',

    NULL,          -- cnpj: não consta em lugar nenhum do sistema hoje. Preencher pela tela.
    '3302700',     -- IBGE de Maricá — mesmo valor já usado em smsmarica.sisreg_configuracao
    'RJ',
    21,            -- DDD assumido ao completar telefone digitado sem ele

    NULL,          -- telefone institucional: não consta. Preencher pela tela.
    'contato@smsmarica.online',   -- canal público já divulgado nas páginas legais do PWA
    'lgpd@smsmarica.online',      -- ver NOTA 2
    '552137315313',               -- número que o app do cidadão exibe

    NULL,          -- logo_midia_id  → ver NOTA 3
    NULL,          -- favicon_midia_id → ver NOTA 3

    NULL,          -- cor_primaria         ⎫
    NULL,          -- cor_secundaria       ⎬ ver NOTA 4 — deixar NULO é intencional
    NULL,          -- cor_gradiente_inicio ⎪
    NULL,          -- cor_gradiente_fim    ⎭

    'https://smsmarica.online',
    'https://app.smsmarica.online',
    'https://arquivos.smsmarica.online',

    NULL,          -- assinatura_produto_html → ver NOTA 5

    NULL,          -- atualizado_por_usuario_id: semeado por script, não por usuário
    now()
)
ON CONFLICT (id) DO NOTHING;


-- ---------------------------------------------------------------------------------
-- Conferência (rodar junto; deve devolver exatamente 1 linha com os textos de Maricá)
-- ---------------------------------------------------------------------------------
SELECT nome_secretaria, nome_curto, uf, codigo_ibge, ddd_padrao,
       cor_primaria, url_painel
FROM   smsmarica.instituicao
WHERE  id = '00000000-0000-0000-0000-00000000fffe';


-- =====================================================================================
-- NOTAS — cada uma é uma escolha, não um descuido
-- =====================================================================================
--
-- NOTA 1 — `nome_curto` = 'Saúde Maricá'
--   Um campo só alimenta dois lugares que hoje divergem:
--     • página pública de verificação  → hoje mostra "Saúde Maricá"
--     • título da aba do painel        → hoje mostra "SMS Maricá — Painel"
--   Escolhi preservar a página pública, que é a que o cidadão vê e está impressa em QR já
--   em circulação. Efeito colateral aceito: a aba do painel passa a ler
--   "Saúde Maricá — Painel" em vez de "SMS Maricá — Painel".
--
-- NOTA 2 — `email_dpo` = 'lgpd@smsmarica.online'
--   É o endereço que consta no termo de consentimento (Core/Cidadao/TermoConsentimento.cs).
--   As páginas legais do PWA divulgam 'contato@smsmarica.online' para o mesmo fim. Os dois
--   estão em produção e divergem — isto aqui apenas reproduz o estado atual, sem escolher
--   por você. Unificar é decisão de quem responde pela LGPD.
--
-- NOTA 3 — logo e favicon nulos
--   Com estes campos nulos o painel continua servindo `/marica_logo.png` do build e o favicon
--   atual — exatamente o que está no ar hoje. Subir os arquivos pela tela é opcional; só
--   passa a ser necessário quando a marca tiver de mudar sem um novo build.
--
-- NOTA 4 — cores nulas (o ponto mais importante deste arquivo)
--   NÃO preencha as cores para Maricá. Informar `cor_primaria` faz o painel DERIVAR toda a
--   escala 50..950 a partir dela, substituindo os onze tons ajustados à mão que estão hoje
--   em `src/index.css`. Os tons derivados ficam próximos, mas não idênticos — seria uma
--   mudança visual gratuita em todo o painel.
--   Com as cores nulas: o painel usa a paleta ajustada à mão, e a página pública usa a cor
--   de marca padrão do produto (#C8102E). A única diferença perceptível para hoje é o título
--   dessa página, que era #C4122F — variação invisível a olho nu, e que passa a usar um
--   vermelho só em todo o sistema.
--
-- NOTA 5 — assinatura do fornecedor nula
--   O modelo de marca escolhido é híbrido (prefeitura + assinatura discreta da Automais), mas
--   hoje NÃO existe assinatura nenhuma na tela de login de Maricá. Ligar isso é acrescentar um
--   elemento visível novo a um sistema em produção — decisão de produto, não de deploy. Fazer
--   pela tela, quando decidido.
-- =====================================================================================
