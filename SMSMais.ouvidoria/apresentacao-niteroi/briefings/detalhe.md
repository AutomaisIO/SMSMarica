# Briefing da tela: Detalhe da manifestação (protocolo)

Rota: /app/ouvidoria/{id}  —  e /app/ouvidoria/meu-ponto/{id} (mesma página com modoPonto)

## Propósito
Tela única da manifestação: cabeçalho com protocolo/badges/prazos, barra de ações conforme status e perfil, relato, resposta conclusiva, manifestante, referido/envolvido, linha do tempo append-only e três blocos de contexto à direita.

## Layout (descrição fiel do código)
Container `space-y-5`. (0) Link "Voltar" (ícone ArrowLeft, `text-sm text-slate-500`). (1) CABEÇALHO em cartão `rounded-xl border border-slate-200 bg-white p-5 shadow-sm`: linha superior `flex flex-wrap items-start justify-between gap-3` — à esquerda o protocolo em `text-lg font-semibold text-slate-900` com botão de copiar (dica "Copiar protocolo") seguido, na mesma linha, das pílulas Tipo, Status, Prioridade e Identificação; abaixo, em `text-xs text-slate-500`: "Registrada em {data} · {canal} · {origem}" e, quando há responsável, " · Responsável: {nome}". À direita, empilhados (`flex flex-col items-end gap-1`), o <PrazoChip> "Cidadão", o <PrazoChip> "Área" (quando Encaminhada ou área atrasada) e, se prorrogada, "Prorrogada em {data}" em `text-xs text-slate-500`. Ainda no cartão, faixas de alerta opcionais: faixa âmbar de duplicidade (ícone Copy) e faixa vermelha de denúncia não habilitada (ícone AlertTriangle). O cartão fecha com uma divisória `border-t border-slate-100 pt-4` e a BARRA DE AÇÕES (botões pequenos lado a lado; primárias em vermelho sólido, demais em outline, "Arquivar" em danger). (2) CORPO em grade `lg:grid-cols-3`: coluna esquerda de 2/3 com os blocos "Relato", "Resposta conclusiva ao cidadão" (cartão verde), "Arquivada" (cartão cinza), "Manifestante", "Paciente referido" + "Agente/serviço envolvido" (lado a lado em sm:grid-cols-2) e "Linha do tempo"; coluna direita de 1/3 com os blocos "Classificação", "Fato e contexto" e "Relógios". Cada bloco é um cartão branco `rounded-xl border border-slate-200 bg-white p-4 shadow-sm` com título `text-sm font-semibold text-slate-700`, e cada item é um par `dt` (`text-xs text-slate-500`) sobre `dd` (`text-slate-800`), com "—" quando vazio. A LINHA DO TEMPO é uma `ol` com borda esquerda `border-l border-slate-200 pl-6`; cada evento tem um círculo de ícone `h-7 w-7 rounded-full ring-4 ring-white`, o nome do evento em negrito, a pílula "visível ao cidadão" (azul, ícone Eye) ou "interno" (cinza, ícone EyeOff), o instante + autor + ponto de resposta em `text-xs text-slate-500`, a transição "{status anterior} → {status novo}" e o texto do evento em `whitespace-pre-wrap`.

## Campos / cartões
- MODAL "Triar": "Tipo" (Select, opções incompatíveis com a identificação ficam disabled; erro "Incompatível com a identificação desta manifestação."); "Prioridade" (Select; dica "Urgente = 2 dias úteis para a área; Alta = 10; Normal = 20 (configurável)."); par "Assunto"/"Subassunto"; "Unidade" (vazio = "Não se aplica"); "Resumo" (dica "Uma linha para a fila (até 200 caracteres)."); "Marcadores" (pílulas clicáveis; marcada = `border-red-300 bg-red-50 text-red-700`); botão "Salvar triagem"
- MODAL "Encaminhar à área": "Ponto de resposta" * (dica "Denúncia só vai para unidade apuratória." ou "Unidade, área central ou apuração que vai responder."); "Prazo para a área (dias)" (number 1–90; dica "Em branco: usa a prioridade e a configuração (ou o prazo do ponto)."); "Orientação à área (opcional)" (textarea 3 linhas; dica "Interna. O cidadão vê só 'Encaminhada à área responsável', sem nomes."); em denúncia, "Teor pseudonimizado" * (textarea 8 linhas; dica "Versão SEM nomes, contatos ou pistas de quem denunciou. É o que a apuração recebe."); botão "Encaminhar"
- MODAL "Responder ao cidadão": radios "Tipo de resposta" — "Conclusiva (encerra a manifestação)" / "Intermediária (só informa o andamento)"; "Resposta ao cidadão" * (textarea 7 linhas) com botão "?" "Conteúdo mínimo da resposta — {Tipo}"; quando conclusiva, "Resolutividade" * (Resolvida / Não resolvida) e "Situação final" * (varia por tipo) e, se "Não atendida", "Motivo do não atendimento" *; botão "Enviar resposta conclusiva" ou "Enviar resposta intermediária"
- MODAL "Arquivar": aviso "Arquivar encerra a manifestação sem resposta de mérito. O cidadão vê \"arquivada\" e o motivo."; "Motivo" * (9 opções); campo seguinte é "Protocolo da manifestação original" (Input, placeholder "AAAA-NNNNNN") quando o motivo é Duplicidade, senão "Observação (opcional)" (textarea); botão "Arquivar"
- MODAL "Encaminhar a outro órgão": faixa âmbar "Depois de encaminhada a outro órgão, a manifestação não pode mais ser prorrogada aqui."; "Órgão / sistema de destino" *; "Protocolo lá (opcional)"; "Informação ao cidadão" * (dica "Vai ao acompanhamento: para onde foi e como acompanhar lá."); botão "Encaminhar"
- MODAIS de texto simples (um textarea + botão "Confirmar"): "Pedir complementação" → label "O que falta o cidadão informar", dica "Vai ao cidadão. O prazo fica suspenso até a resposta (só uma vez por manifestação).", mínimo 10; "Devolver à área" → "O que precisa ser reanalisado", dica "Interna. A área ganha novo prazo (metade do original, mínimo 2 dias)."; "Prorrogar prazo" → "Justificativa da prorrogação", dica "Vai ao cidadão. Só é possível prorrogar uma vez; mínimo de 20 caracteres."; "Escalonar" → "Para quem e por quê", dica "Interna. Registra que a manifestação subiu de nível (gestão)."; "Registrar recurso" → "Razões do recurso do cidadão", dica "Vai ao acompanhamento. Só cabe um recurso por manifestação."; "Habilitar denúncia" → "Análise de admissibilidade", dica "Interna. Autoria, materialidade e competência: por que a denúncia segue para apuração."; "Editar teor pseudonimizado" → "Teor pseudonimizado", dica "É esta versão que a unidade apuratória recebe. Retire nomes, contatos e qualquer pista de quem denunciou."; "Anotar" → "Anotação interna", dica "Fica só na trilha interna; o cidadão não vê."
- MODAIS com texto + anexos: "Registrar complementação" → "Complementação recebida", dica "O que o cidadão trouxe (por telefone, presencial, e-mail…). O relógio do prazo volta a contar."; "Responder pela área" → "Resposta da área", dica "Interna: a ouvidoria valida antes de responder ao cidadão. Diga o que foi apurado e o que foi feito."; ambos com campo "Anexos (opcional)"
- MODAL "Cobrar a área": "Mensagem à área (opcional)" (textarea 4 linhas; dica "Interna. Fica registrado que a área foi cobrada; os membros do ponto veem na trilha."); botão "Registrar cobrança"

## Badges e chips
- Tipo, Status, Prioridade e Identificação (mesmas pílulas da fila)
- PrazoChip "Cidadão:" e "Área:"
- Faixa âmbar de duplicidade: "Possível duplicidade: mesmo CPF, assunto e unidade nos últimos 90 dias em **{protocolos}**. Confira antes de encaminhar; arquivar por duplicidade é decisão sua."
- Faixa vermelha: "Denúncia ainda não habilitada: só vai à apuração depois da análise de admissibilidade (módulo Sigilo)."
- Marcadores: pílulas `rounded-full bg-slate-100 px-2 py-0.5 text-xs text-slate-700`
- Linha do tempo: pílula `bg-sky-50 text-sky-700` com "visível ao cidadão" ou `bg-slate-100 text-slate-500` com "interno"

## Ações
- Barra de botões (ROTULO_ACAO), na ordem em que a regra os produz: "Triar", "Encaminhar à área", "Pedir complementação", "Registrar complementação", "Responder pela área", "Devolver à área", "Responder ao cidadão", "Prorrogar prazo", "Cobrar a área", "Registrar recurso", "Concluir", "Encaminhar a outro órgão", "Anotar", "Escalonar", "Arquivar", "Habilitar denúncia", "Editar teor pseudonimizado"
- Destaque: PRIMARIAS (botão vermelho sólido) = Triar, Encaminhar à área, Responder pela área, Responder ao cidadão, Registrar complementação, Concluir. PERIGOSAS (vermelho danger) = Arquivar. Todas as outras em outline
- Quando não há ação disponível: "Nenhuma ação disponível para você neste status."
- "Revelar identidade" (botão outline com ícone KeyRound) no bloco Manifestante, quando a identidade é restrita
- Link "Abrir cadastro" (ícone ExternalLink) no bloco Paciente referido; link "Abrir pedido" quando há regulação vinculada
- Bloco "Relato" traz, em denúncia e fora do modoPonto, um <details> "Teor pseudonimizado (o que a apuração recebe)"
- "Voltar" → /app/ouvidoria (ou /app/ouvidoria/meu-ponto em modoPonto)

## Modais
- "Triar" (largura lg)
- "Encaminhar à área" (md)
- "Pedir complementação" (md)
- "Registrar complementação" (md)
- "Responder pela área" (md)
- "Devolver à área" (md)
- "Responder ao cidadão" (lg)
- "Prorrogar prazo" (md)
- "Cobrar a área" (md)
- "Escalonar" (md)
- "Registrar recurso" (md)
- "Arquivar" (md)
- "Encaminhar a outro órgão" (md)
- "Habilitar denúncia" (md)
- "Editar teor pseudonimizado" (md)
- "Anotar" (md)
- ConfirmDialog "Concluir manifestação" — mensagem "A manifestação já foi respondida e não houve recurso. Concluir encerra o ciclo (o sistema faria isso sozinho após o prazo de recurso).", botão "Concluir"
- "Revelar identidade do manifestante" (sm) — descrição "Acesso excepcional a dado protegido. Só faça se for indispensável ao tratamento."; faixa âmbar com ícone ShieldAlert: "**Este acesso fica registrado com seu nome e data**, junto com a justificativa, na trilha da manifestação e na auditoria do sistema."; campo "Justificativa" * (textarea 3 linhas, máx. 500, dica "Mínimo de 15 caracteres. Diga por que precisa ver a identidade."); botões "Cancelar" e "Revelar"

## Textos literais do JSX
- Voltar
- Registrada em
- Responsável:
- Prorrogada em
- Possível duplicidade: mesmo CPF, assunto e unidade nos últimos 90 dias em
- Confira antes de encaminhar; arquivar por duplicidade é decisão sua.
- Denúncia ainda não habilitada: só vai à apuração depois da análise de admissibilidade (módulo Sigilo).
- Nenhuma ação disponível para você neste status.
- Relato
- Relato (versão pseudonimizada)
- Teor pseudonimizado (o que a apuração recebe)
- Ainda não preenchido — obrigatório para encaminhar.
- Anexos
- Resposta conclusiva ao cidadão
- Resolutividade
- Situação final
- Respondida em
- Motivo do não atendimento
- Arquivada
- Manifestante
- Manifestação anônima — sem dados do manifestante e sem código de acesso.
- Identidade restrita.
- Revelar identidade
- Identidade revelada nesta sessão — o acesso ficou registrado com seu nome e data.
- Revelar identidade do manifestante
- Acesso excepcional a dado protegido. Só faça se for indispensável ao tratamento.
- Este acesso fica registrado com seu nome e data
- Justificativa
- Mínimo de 15 caracteres. Diga por que precisa ver a identidade.
- Cancelar
- Revelar
- Paciente referido
- Nome
- CPF
- CNS
- Abrir cadastro
- Agente/serviço envolvido
- Linha do tempo
- Nenhum evento registrado.
- Linha do tempo da manifestação
- visível ao cidadão
- interno
- O cidadão vê este evento no acompanhamento
- Evento interno — o cidadão não vê
- Classificação
- Assunto
- Subassunto
- Unidade
- Ponto de resposta
- Marcadores
- Fato e contexto
- Data do fato
- Local do fato
- Sistema externo
- Protocolo externo
- Pedido de regulação vinculado
- Abrir pedido
- Relógios
- Prazo ao cidadão
- Prazo da área
- Encaminhada em
- Concluída em
- Complementação
- Já pedida (uma vez)
- Não pedida
- Justificativa da prorrogação
- Denúncia habilitada em
- Última atividade
- Triar
- Encaminhar à área
- Pedir complementação
- Registrar complementação
- Responder pela área
- Devolver à área
- Responder ao cidadão
- Prorrogar prazo
- Cobrar a área
- Escalonar
- Registrar recurso
- Concluir
- Arquivar
- Encaminhar a outro órgão
- Habilitar denúncia
- Editar teor pseudonimizado
- Anotar
- Confirmar
- Salvando…
- Salvar triagem
- Encaminhar
- Registrar cobrança
- Enviar resposta conclusiva
- Enviar resposta intermediária
- Tipo de resposta
- Conclusiva (encerra a manifestação)
- Intermediária (só informa o andamento)
- Concluir manifestação
- A manifestação já foi respondida e não houve recurso. Concluir encerra o ciclo (o sistema faria isso sozinho após o prazo de recurso).
- Manifestação concluída.
- Triagem salva.
- Encaminhada à área.
- Pedido de complementação registrado.
- Complementação registrada.
- Resposta da área registrada.
- Devolvida à área.
- Resposta ao cidadão registrada.
- Prazo prorrogado.
- Cobrança registrada.
- Escalonamento registrado.
- Recurso registrado.
- Manifestação arquivada.
- Encaminhada a outro órgão.
- Denúncia habilitada.
- Teor pseudonimizado atualizado.
- Anotação registrada.
- Manifestação não encontrada.
- Carregando…
