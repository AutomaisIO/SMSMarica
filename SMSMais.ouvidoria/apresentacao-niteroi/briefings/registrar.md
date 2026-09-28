# Briefing da tela: Registrar manifestação

Rota: /app/ouvidoria/registrar

## Propósito
Registro interno pela ouvidoria de uma manifestação que chegou por canal não-digital (presencial, telefone, carta…). Ao salvar, abre o modal com protocolo e código de acesso.

## Layout (descrição fiel do código)
Container centralizado `mx-auto max-w-4xl space-y-5`. No topo, um link-botão discreto `inline-flex items-center gap-1 text-sm text-slate-500` com ícone `ArrowLeft h-4 w-4` + "Voltar à fila". Abaixo, o bloco de título: `h1.flex.items-center.gap-2.text-xl.font-semibold.text-slate-800` com ícone `FilePlus2 h-5 w-5 text-red-600` + "Registrar manifestação", o "?" do manual (artigo="ouvidoria", secao="registrar") ao lado, e o subtítulo `p.text-sm.text-slate-500`. Em seguida o formulário `space-y-6`, dividido em 4 <fieldset> "Seção", cada um um cartão branco `rounded-xl border border-slate-200 bg-white p-4 shadow-sm` com <legend> `px-1 text-sm font-semibold text-slate-700` e, opcionalmente, uma descrição `-mt-1 text-xs text-slate-500`. Dentro das seções os campos usam <Campo> (label `.label` + asterisco vermelho quando obrigatório, campo, e abaixo a dica em `text-xs text-gray-500` ou o erro em `text-xs text-error-600`). Rodapé do formulário: `div.flex.justify-end.gap-2` com o botão primário de submit (ícone Send, ou Loader2 girando).

## Campos / cartões
- SEÇÃO 1 — "O que o cidadão traz" — grade de 2 colunas (sm:grid-cols-2):
- 1. "Tipo" * (Select id=ouv-tipo) — opções: Solicitação / Reclamação / Denúncia / Sugestão / Elogio / Informação. Padrão: Reclamação. Dica varia por tipo: Solicitação="Pedido de atendimento, exame, medicamento, transporte… Sempre identificada."; Reclamação="Insatisfação com um serviço ou atendimento prestado."; Denúncia="Relato de irregularidade ou ilícito. Pode ser sigilosa ou anônima."; Sugestão="Ideia para melhorar um serviço."; Elogio="Reconhecimento a um serviço ou profissional."; Informação="Pedido de informação sobre serviços, horários, fluxos. Sempre identificada."
- 2. "Identificação" * (Select id=ouv-identificacao) — opções: "Identificada", "Sigilosa (identidade restrita à ouvidoria)", "Anônima (sem acompanhamento)"; as não permitidas para o tipo ficam disabled. Dica: quando só cabe uma → "{Tipo} é sempre identificada."; em Denúncia → "Anônima vira comunicação de irregularidade: sem código de acesso e sem acompanhamento."; nos demais → "Sigilosa: só a ouvidoria vê quem é; a área responde sem saber."
- 3. "Canal de entrada" * (Select id=ouv-canal) — 14 opções na ordem: "Painel (registro interno)", "Presencial", "Telefone", "WhatsApp", "E-mail", "Carta", "Urna", "Busca ativa", "Site público", "App do cidadão", "Disque 136", "Fala.BR", "Ouvidoria-geral do município", "Outro". Padrão: Presencial
- 4. "Origem" * (Select id=ouv-origem) — opções: "Cidadão", "Ouvidoria ativa", "De ofício", "Coletiva". Padrão: Cidadão
- 5. "Relato (teor)" * (Textarea id=ouv-teor, rows=6, maxLength=10000) — dica: "Escreva como o cidadão contou. Não há campo 'motivo' — o texto é o registro."; erro se < 10 caracteres: "Descreva a manifestação com pelo menos 10 caracteres."
- 6. "Resumo (opcional)" (Input id=ouv-resumo, maxLength=200) — dica: "Uma linha para a fila. Até 200 caracteres."; erro: "Máximo de 200 caracteres."
- 7. Par encadeado <SeletorAssunto> (grade 2 colunas): label "Assunto" (Select id=ouv-assunto-pai, opção vazia "Selecione") e label "Subassunto" (Select id=ouv-assunto-sub, opção vazia "Escolha o assunto" / "Sem subassuntos" / "Opcional")
- 8. Grade de 3 colunas: "Unidade relacionada" (SeletorUnidade id=ouv-unidade, opção vazia "Não se aplica / não sei"); "Data do fato" (Input type=date id=ouv-data-fato, max=hoje); "Local do fato" (Input id=ouv-local-fato, maxLength=200, placeholder "Setor, sala, recepção…")
- SEÇÃO 2 — "Quem manifesta" — descrição condicional: se Anônima → "Manifestação anônima: nenhum dado do manifestante é registrado."; se Sigilosa → "Os dados ficam restritos à ouvidoria. A área que responde não os vê."
- 9. Bloco de busca: label "Buscar cidadão já cadastrado (opcional)" com botão de ajuda "?" cujo título é "Buscar cidadão" e texto "Preenche nome, CPF e telefone a partir do cadastro. Você pode ajustar depois."; campo <BuscaPaciente> com placeholder "Nome ou CPF do manifestante"
- 10. Grade 2 colunas: "Nome" (Input id=ouv-m-nome, maxLength=200); "CPF" (Input id=ouv-m-cpf, inputMode numeric, maxLength=14, placeholder "000.000.000-00"); "Telefone" (Input id=ouv-m-telefone, maxLength=20, placeholder "(21) 9xxxx-xxxx", dica "Com DDD. É por aqui que o cidadão recebe o protocolo e as etapas (WhatsApp)."); "E-mail" (Input type=email id=ouv-m-email, maxLength=200)
- 10b. Quando Anônima, a seção inteira vira uma linha com ícone Info: "Sem código de acesso: o cidadão não poderá acompanhar nem complementar."
- SEÇÃO 3 — "Em favor de quem / sobre quem" — descrição "Só quando a manifestação é sobre outra pessoa (paciente) ou aponta um agente."
- 11. Label "Paciente referido — buscar no cadastro (opcional)" + <BuscaPaciente> com placeholder "Nome ou CPF do paciente em favor de quem se manifesta"
- 12. Grade 3 colunas: "Nome do paciente" (id=ouv-r-nome, maxLength=200); "CPF" (id=ouv-r-cpf, maxLength=14); "CNS" (id=ouv-r-cns, maxLength=15)
- 13. "Agente/serviço envolvido" (Input id=ouv-envolvido, maxLength=300) — dica: "Descreva como o cidadão identificou (nome, função, setor). Em denúncia, este dado não vai à área na versão pseudonimizada."
- SEÇÃO 4 — "Complementos"
- 14. Grade 2 colunas: "Sistema externo" (Input id=ouv-sistema-externo, maxLength=40, dica "Ex.: OuvidorSUS, Fala.BR, ouvidoria-geral."); "Protocolo externo" (Input id=ouv-protocolo-externo, maxLength=60)
- 15. "Anexos" — botão outline com ícone Paperclip "Anexar arquivo" (ou "Enviando…" com spinner) + texto "Imagens ou PDF, até 6 MB cada."; cada anexo vira uma pílula `rounded-lg border border-slate-200 bg-slate-50 px-2 py-1 text-xs` com ícone FileText, o nome truncado e um X (aria-label "Remover {arquivo}")

## Ações
- "Voltar à fila" (ícone ArrowLeft) → /app/ouvidoria
- Submit: botão primário "Registrar manifestação" (ícone Send); enquanto envia mostra "Registrando…" com spinner
- "Anexar arquivo" → abre o seletor de arquivos (image/*, application/pdf, múltiplo, limite 6 MB por arquivo)
- Ao escolher um paciente na busca, os campos de nome/CPF/telefone são preenchidos automaticamente
- Botão "?" ao lado do título → artigo "ouvidoria", seção "registrar"

## Modais
- "Manifestação registrada" (largura sm) — mostrado depois de salvar: (a) faixa verde `border-green-200 bg-green-50 text-green-800` com ícone CheckCircle2: "Registrada com sucesso. Prazo de resposta ao cidadão: **{data}**."; (b) caixa `rounded-lg border border-slate-200 p-3` com o rótulo pequeno "Protocolo" e o número em `text-lg font-semibold text-slate-900` com botão de copiar (dica "Copiar protocolo"); (c) caixa igual com "Código de acesso (para o cidadão acompanhar)" e o código em `tracking-widest` (dica "Copiar código de acesso"); (d) faixa âmbar com ícone AlertTriangle: "**Guarde e repasse este código agora** — ele não será mostrado de novo. O sistema guarda apenas uma versão cifrada."; (e) se anônima, no lugar de (c)+(d): "Manifestação anônima: não há código de acesso e o manifestante **não terá acompanhamento**. Informe só o protocolo, se ele quiser guardar."; rodapé com botão ghost "Registrar outra" e botão primário "Abrir manifestação"

## Textos literais do JSX
- Voltar à fila
- Registrar manifestação
- Só tipo, identificação, canal e relato são obrigatórios. Nada é recusado por falta dos demais campos.
- O que o cidadão traz
- Tipo
- Identificação
- Canal de entrada
- Origem
- Relato (teor)
- Resumo (opcional)
- Assunto
- Subassunto
- Selecione
- Escolha o assunto
- Sem subassuntos
- Opcional
- Unidade relacionada
- Não se aplica / não sei
- Data do fato
- Local do fato
- Setor, sala, recepção…
- Quem manifesta
- Manifestação anônima: nenhum dado do manifestante é registrado.
- Os dados ficam restritos à ouvidoria. A área que responde não os vê.
- Buscar cidadão já cadastrado (opcional)
- Buscar cidadão
- Preenche nome, CPF e telefone a partir do cadastro. Você pode ajustar depois.
- Nome ou CPF do manifestante
- Nome
- CPF
- 000.000.000-00
- Telefone
- Com DDD. É por aqui que o cidadão recebe o protocolo e as etapas (WhatsApp).
- (21) 9xxxx-xxxx
- E-mail
- Sem código de acesso: o cidadão não poderá acompanhar nem complementar.
- Em favor de quem / sobre quem
- Só quando a manifestação é sobre outra pessoa (paciente) ou aponta um agente.
- Paciente referido — buscar no cadastro (opcional)
- Nome ou CPF do paciente em favor de quem se manifesta
- Nome do paciente
- CNS
- Agente/serviço envolvido
- Descreva como o cidadão identificou (nome, função, setor). Em denúncia, este dado não vai à área na versão pseudonimizada.
- Complementos
- Sistema externo
- Ex.: OuvidorSUS, Fala.BR, ouvidoria-geral.
- Protocolo externo
- Anexos
- Anexar arquivo
- Enviando…
- Imagens ou PDF, até 6 MB cada.
- Registrando…
- Escreva como o cidadão contou. Não há campo 'motivo' — o texto é o registro.
- Uma linha para a fila. Até 200 caracteres.
- Descreva a manifestação com pelo menos 10 caracteres.
- Máximo de 200 caracteres.
- CPF inválido.
- E-mail inválido.
- Manifestação identificada: informe o CPF, ou o nome com telefone/e-mail.
- Manifestação identificada precisa de ao menos nome, CPF ou um contato.
- Manifestação registrada
- Registrada com sucesso. Prazo de resposta ao cidadão:
- Código de acesso (para o cidadão acompanhar)
- Guarde e repasse este código agora
- — ele não será mostrado de novo. O sistema guarda apenas uma versão cifrada.
- Manifestação anônima: não há código de acesso e o manifestante
- não terá acompanhamento
- Informe só o protocolo, se ele quiser guardar.
- Registrar outra
- Abrir manifestação
- Copiar protocolo
- Copiar código de acesso
