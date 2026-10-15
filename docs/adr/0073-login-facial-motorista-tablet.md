# ADR-0073 — Login do motorista no tablet do veículo pelo rosto (primeiro acesso pelo WhatsApp, só o escalado do dia, liberação pela central)

**Status:** proposto · **Data:** 2026-10-15
**Relacionado:** [ADR-0003](./0003-flutter-android-only-agente.md) (app do agente só Android) ·
[ADR-0045](./0045-criterio-para-extrair-servico.md) (motorista é papel de `Usuario`, FK `motorista.usuario_id`) ·
[ADR-0036](./0036-login-cidadao-contato-verificado.md) (código só para contato verificado) ·
[ADR-0018](./0018-identidade-e-sessao-do-cidadao.md) (sessão do cidadão) ·
pedido de backend de junho em [`docs/modulos/tfd/backend-app-motorista.md`](../modulos/tfd/backend-app-motorista.md) ·
[`docs/modulos/tfd/deslocamento-tablet.md`](../modulos/tfd/deslocamento-tablet.md) (o tablet é do veículo)

## Contexto

O tablet do TFD é **do veículo**, não do motorista: fica preso no carro, em paisagem, em modo quiosque
pelo MDM, e fala com o servidor por um token próprio (`X-Dispositivo-Token`) ligado ao veículo. Os
aparelhos da frota (Qualcomm msm8953, Android 9) **não têm Play Services funcionando**, e o carro passa
parte do dia sem rede.

Até aqui o login do app era de mentira (`mock-001`), e o motorista criado no painel nasce com
`SenhaHash = "PENDENTE_AUTH"`: não entra em lugar nenhum. A central não sabe quem estava dirigindo cada
translado, e o REPORT e as posições ficam só com o veículo.

O app já tinha o esqueleto do reconhecimento facial no próprio aparelho (ML Kit + MobileFaceNet em
TFLite + piscada + armazenamento cifrado), nunca ligado. Em 15/10/2026 ele foi provado no tablet da
frota (`501cd033`):

- o detector do ML Kit **com o modelo dentro do APK** (`com.google.mlkit:face-detection`) e o TFLite
  próprio do app funcionam sem Play Services;
- a mesma pessoa tirou nota **0,94 a 0,96** em 5 verificações contra o próprio cadastro (limiar 0,62);
- a prova de vida levou **4 a 10 s** e o reconhecimento, **0,6 a 0,8 s** depois da foto.

O pedido de junho propunha JWT de motorista, verificação gravada por RBAC e OTP no primeiro acesso. O
tablet, porém, não tem usuário logado no servidor e precisa funcionar sem rede, então o desenho abaixo
troca o JWT pelo token do veículo.

## Decisão

1. **O rosto é comparado no tablet; o servidor não compara rostos.** Detector do ML Kit **embutido**
   (nunca a variante que depende do Play Services), vetor de identidade MobileFaceNet em TFLite,
   similaridade de cosseno. Limiar inicial **0,62**, recalibrado com o teste de pessoa diferente antes
   de ir para a frota. No tablet fica só o vetor, cifrado (SQLCipher, chave no Keystore).

2. **Prova de vida ativa.** Piscar, virar o rosto para um lado **sorteado** e voltar de frente, com o
   mesmo rosto do começo ao fim (`trackingId` do detector) e prazo por etapa. A foto que vai para a
   comparação é a do fim, de frente. Só a piscada, um vídeo da pessoa piscando passaria.

3. **Só entra o motorista escalado do dia naquele veículo.** "Escalado" = motorista da `RotaDiaria` do
   veículo do tablet com `Data` = hoje no fuso de Brasília e status diferente de Cancelada. O tablet
   baixa, pelo token do veículo, a lista dos escalados do dia e a **foto de referência** de cada um, gera
   o vetor e compara contra eles (normalmente um só). Sem rede, usa a última lista baixada, que vale
   apenas para o dia dela.

4. **Primeiro acesso pelo próprio motorista, com código no WhatsApp.** Quando o escalado ainda não tem
   foto de referência:
   1. o motorista digita o CPF no tablet;
   2. o servidor confere que o CPF é de um motorista escalado hoje naquele veículo; se não for, a
      resposta não diz por quê (não revela quem existe);
   3. sai um código de 6 dígitos para o **telefone do cadastro do motorista**, pelo mesmo modelo de
      mensagem de autenticação do app do cidadão (`authzap`), válido por 5 minutos e com 5 tentativas;
      o modo de teste (`Tfd:Otp:ModoTeste`) mostra o código na tela;
   4. confirmado o código, o motorista lê o termo de ciência e aceita;
   5. faz a captura com prova de vida;
   6. a foto vai para o servidor como **foto de referência facial**, num campo próprio, separado da
      foto de perfil.

   O telefone foi cadastrado pelo gestor no vínculo de trabalho. Receber o código prova que quem está no
   tablet tem esse telefone, que é o espírito do ADR-0036. O número **não** é carimbado como verificado:
   o serviço de validação de telefone procura pelo CPF e é do paciente, e poderia marcar a ficha de
   paciente da mesma pessoa. Limite de um pedido de código por minuto por CPF.
   Os outros tablets baixam a mesma referência: **o motorista cadastra o rosto uma vez só**.

5. **Sem reconhecimento, a central libera.** Se o rosto não confere, não há escala no dia, a câmera
   falha ou o primeiro acesso não chega, a central gera no painel um **código de uso único** para aquele
   veículo e aquele motorista, válido por 15 minutos. O motorista digita no tablet e entra. Fica
   registrado quem gerou, quando e em que tablet foi usado. O motorista nunca fica preso no quiosque.

6. **Sessão local, sem JWT de motorista.** O tablet continua falando só pelo token do veículo. Cada
   verificação — login, início de translado, primeiro acesso, liberação pela central —, aprovada ou
   não, vira um registro no servidor com:
   - nota e limiar usado;
   - resultado da prova de vida e versão do modelo;
   - posição e selfie reduzida.

   O registro é enviado pelo token do veículo, sem duplicar (`idLocal` gerado no tablet), e usa uma
   fila sem rede como a do REPORT. "Quem está dirigindo" é o último registro aprovado do veículo.

7. **Confere de novo antes de cada translado**, antes de abrir a navegação. Funciona sem rede.

8. **LGPD.** Biometria é dado pessoal sensível (art. 5º, II).
   - **Base legal:** a base para identificação e autenticação é o art. 11, II, "g" (prevenção à fraude
     e segurança do titular), e precisa ser confirmada pelo encarregado da Secretaria.
   - **Termo:** o aceite de ciência é registrado com versão e hash do texto, no mesmo modelo de
     `CidadaoConsentimento`.
   - **Foto de referência:** fica no servidor porque serve a todos os tablets. "Apagar biometria" no
     painel a apaga, e o motorista refaz o primeiro acesso. A exclusão do motorista também apaga.
   - **Selfies de auditoria:** guardadas por **90 dias** (proposta, a confirmar com a Secretaria) e
     apagadas por rotina diária.
   - **Logs:** nunca levam CPF, código ou imagem.

## Consequências

- A central precisa escalar o motorista na rota do dia antes da saída. Sem escala, só a liberação
  pela central resolve.
- O tablet precisa de rede ao menos uma vez no dia para baixar a escala e as fotos de referência.
- O telefone do cadastro do motorista precisa estar certo; se estiver errado, a central libera e
  corrige o cadastro.
- Ataques que imitam a reação na hora (vídeo controlado, máscara) não são cobertos. É um risco aceito
  para o que se protege, e a selfie da auditoria permite conferir depois.
- Do pedido de junho: a foto de referência (BE-1) e a auditoria (BE-2) entram, mas pelo token do
  veículo, não por JWT. O código no WhatsApp (BE-4) fica só para o primeiro acesso. O "iniciar
  translado só com verificação" no servidor (BE-3) fica para depois: a conferência é feita no tablet e
  só registrada no servidor.
- Endpoints novos ficam sob `rastreamento/dispositivos/*` (token do veículo) e, no painel, sob
  `motoristas/*` e `rastreamento/verificacoes-faciais` (permissão do módulo Rastreamento e de
  Motoristas).
