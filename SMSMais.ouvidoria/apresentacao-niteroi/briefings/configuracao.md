# Briefing da tela: Configuração da ouvidoria

Rota: /app/ouvidoria/configuracao

## Propósito
Prazos legais e regras de aviso da ouvidoria desta instância (singleton no backend): sete prazos numéricos, o interruptor de WhatsApp e o texto do recibo.

## Layout (descrição fiel do código)
Container estreito centralizado `mx-auto max-w-3xl space-y-5`. (1) CABEÇALHO: `h1` com ícone `Settings2 h-5 w-5 text-red-600` + "Configuração da ouvidoria", "?" do manual (secao="gestao") e subtítulo. (2) FORMULÁRIO `space-y-5` com dois cartões-fieldset brancos `rounded-xl border border-slate-200 bg-white p-4 shadow-sm`. O primeiro, <legend> "Prazos", é uma grade de 2 colunas (`sm:grid-cols-2`) com os sete campos numéricos, cada um com rótulo visível e dica abaixo em `text-xs text-gray-500`. O segundo, <legend> "Avisos ao cidadão", traz o checkbox de WhatsApp, um parágrafo de advertência em `text-xs text-slate-500` e o campo de texto do recibo (Textarea de 4 linhas, máx. 1000). (3) Eventual faixa de erro vermelha. (4) Rodapé `flex justify-end` com o botão primário de submit (ícone Save) — ou, sem permissão de edição, apenas a frase em `text-xs text-slate-500`.

## Campos / cartões
- 1. "Prazo de resposta ao cidadão (dias)" (number 1–90) — dica "Lei 13.460 art. 16: 30 dias."
- 2. "Prorrogação (dias)" (number 1–90) — dica "Uma vez por manifestação, com justificativa. Padrão 30."
- 3. "Prazo da área — prioridade Normal (dias)" (number 1–60) — dica "Quanto a unidade/área tem para responder. Padrão 20."
- 4. "Prazo da área — prioridade Alta (dias)" (number 1–60) — dica "Padrão 10."
- 5. "Prazo da área — Urgente (dias ÚTEIS)" (number 1–30) — dica "Conta seg–sex. Padrão 2."
- 6. "Prazo para o cidadão complementar (dias)" (number 1–60) — dica "Sem resposta, arquiva automaticamente. Padrão 20."
- 7. "Conclusão automática após resposta (dias)" (number 1–90) — dica "Respondida sem recurso vira Concluída. Padrão 30."
- 8. Checkbox "Avisar por WhatsApp (recibo, encaminhamento, complementação, prorrogação, resposta, arquivamento)"
- 9. Advertência: "Nunca em manifestação anônima. Em sigilosa, o texto leva só protocolo e etapa — nunca teor ou assunto."
- 10. "Texto do recibo" (Textarea id=cfg-recibo, 4 linhas, máx. 1000) — dica "Em branco usa o padrão. Use {protocolo} e {prazo}; são substituídos no envio." (as chaves aparecem em <code>); placeholder "Sua manifestação foi registrada com o protocolo {protocolo}. Prazo de resposta: {prazo}."

## Ações
- Botão primário "Salvar" (ícone Save); durante o envio, "Salvando…" com spinner; ao concluir notifica "Configuração salva."
- Validação local antes de enviar: se algum prazo estiver fora da faixa, mostra a faixa vermelha com "{rótulo do campo}" deve ser um inteiro entre {min} e {max}.
- Botão "?" → artigo "ouvidoria", seção "gestao"

## Textos literais do JSX
- Configuração da ouvidoria
- Prazos legais e regras de aviso. Vale para toda a instância.
- Prazos
- Prazo de resposta ao cidadão (dias)
- Lei 13.460 art. 16: 30 dias.
- Prorrogação (dias)
- Uma vez por manifestação, com justificativa. Padrão 30.
- Prazo da área — prioridade Normal (dias)
- Quanto a unidade/área tem para responder. Padrão 20.
- Prazo da área — prioridade Alta (dias)
- Padrão 10.
- Prazo da área — Urgente (dias ÚTEIS)
- Conta seg–sex. Padrão 2.
- Prazo para o cidadão complementar (dias)
- Sem resposta, arquiva automaticamente. Padrão 20.
- Conclusão automática após resposta (dias)
- Respondida sem recurso vira Concluída. Padrão 30.
- Avisos ao cidadão
- Avisar por WhatsApp (recibo, encaminhamento, complementação, prorrogação, resposta, arquivamento)
- Nunca em manifestação anônima. Em sigilosa, o texto leva só protocolo e etapa — nunca teor ou assunto.
- Texto do recibo
- Em branco usa o padrão. Use
- e
- ; são substituídos no envio.
- {protocolo}
- {prazo}
- Sua manifestação foi registrada com o protocolo {protocolo}. Prazo de resposta: {prazo}.
- Salvar
- Salvando…
- Configuração salva.
- Você pode ver a configuração, mas não alterá-la.
- deve ser um inteiro entre
- Carregando…
