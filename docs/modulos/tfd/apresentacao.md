# Apresentação Executiva — Módulo TFD

**Transporte sanitário inteligente para o Tratamento Fora do Domicílio**

> Material de apresentação (formato slides) para a **Secretaria Municipal de Saúde de
> Maricá** e a **OS Avante — A/C Sra. Clarisse**.
> Documentos de apoio: [`escopo.md`](./escopo.md) · [`cronograma.md`](./cronograma.md) ·
> [`arquitetura.md`](./arquitetura.md) · [`requisitos.md`](./requisitos.md).

---

## Slide 1 — Capa

**Módulo TFD — SMSMarica**
Transporte sanitário inteligente de pacientes
SMS Maricá · OS Avante (A/C Sra. Clarisse)
v1 — 2026-06-18

---

## Slide 2 — O que é o TFD

> **Tratamento Fora do Domicílio (SUS):** transporte de pacientes que precisam de
> atendimento **em outra cidade**, quando o procedimento não está disponível em Maricá.

Todos os dias, **vários carros** levam **dezenas de pacientes** (e acompanhantes) a
**hospitais e unidades de referência** em outras cidades — e os trazem de volta.

**Hoje isso é planejado na mão.** Este projeto torna o processo **inteligente, rastreável
e comunicado**.

---

## Slide 3 — As dores de hoje

- 🧩 Montar carros e rotas **manualmente** — lento e sujeito a erro.
- 🛣️ Rotas **não otimizadas** — mais tempo, mais combustível, mais atraso.
- 🪑 **Acompanhante descoberto na hora** — falta ou sobra assento.
- 📵 **Paciente sem informação** — não sabe o horário nem onde o carro está.
- ⏳ **Volta desorganizada** — pacientes esperam horas após o atendimento.
- 🔍 **Pouca rastreabilidade** — difícil saber onde está cada veículo.

---

## Slide 4 — A solução em uma frase

> Com **um clique**, o sistema **distribui os pacientes nos carros** e gera as **rotas
> otimizadas de coleta e de entrega**; **confirma o acompanhante pelo WhatsApp**; entrega
> ao **motorista no tablet** a rota pronta; e mostra **tudo ao vivo** ao gestor e ao
> paciente.

Tecnologia: **Google Maps** (rotas reais) + **Claude/IA** (distribuição inteligente) +
**WhatsApp oficial** (comunicação) — sobre a plataforma SMSMarica que **já existe**.

---

## Slide 5 — O que muda, na prática

| Para o **gestor/regulador** | Para o **motorista** | Para o **paciente** |
|---|---|---|
| Gera o dia com 1 clique | Recebe a rota pronta no tablet | É avisado pelo WhatsApp |
| Vê as rotas no mapa | Navega direto (Maps/Waze) | Confirma acompanhante |
| Acompanha a frota ao vivo | Recebe pacientes liberados | Vê o carro chegando (ETA) |
| Vê quem está aguardando | **Puxa** quem está perto | Mais autonomia e respeito ao tempo |

---

## Slide 6 — As 8 entregas-chave

1. **Destino por tratamento** (geolocalizado).
2. **Acompanhante** confirmado pelo paciente (WhatsApp).
3. **Geração inteligente** de translado (distribuição + rotas).
4. **Rotas otimizadas** de **coleta** e de **entrega**.
5. **App do motorista (tablet)** com rota, navegação e "puxar".
6. **"Puxar" pacientes** que aguardam fora de Maricá (por **distância**).
7. **WhatsApp** do paciente (confirmação, coleta, chegada).
8. **App do cidadão** + **painel ao vivo** para o gestor.

---

## Slide 7 — Já temos meio caminho andado

A plataforma SMSMarica **já possui**: cadastros (motoristas, veículos, unidades,
tratamentos, pacientes), **rotas diárias**, **mapa de assentos** (com assento de
acompanhante), **periodicidade e sessões**, **ingestão de GPS e geofences**, **Claude já
integrado** e o **app do motorista iniciado**.

➡️ Por isso conseguimos entregar um **piloto operacional em 20 dias corridos**.

---

## Slide 8 — Cronograma (20 dias corridos · 24/06 → 13/07/2026)

> Gráfico de Gantt completo em [`proposta-tfd.html`](./proposta-tfd.html) (seção 9).

| Fase | Datas | Entrega |
|------|-------|---------|
| **1 — Ajuste + Fundação** | 24–28/06 | Destino no mapa · acompanhante · **conta Meta + base WhatsApp** |
| **2 — Núcleo** | 29/06–04/07 | **Geração de translado** (distribuição + rotas) |
| **3 — Apps + tempo real** | 05–10/07 | App motorista (tablet) · "puxar" · app cidadão |
| **4 — Teste → Piloto → Implantação** | 11–13/07 | **Piloto assistido** + plano de expansão |

🏁 **Marcos:** M1 · 28/06 · M2 · 04/07 · M3 · 10/07 · **M4 Go/No-Go · 13/07**

---

## Slide 9 — Como funciona a "mágica"

```
   Pacientes do dia + acompanhantes + frota
                     │
        Google Maps  ▼  Claude (IA)
   (distâncias e ──▶ distribui nos carros ◀── decide com
    rotas reais)     e otimiza as rotas      regras de negócio
                     │
                     ▼
   Rota de COLETA (pegar em Maricá) + rota de ENTREGA (destinos) + RETORNO
                     │
        ┌────────────┼─────────────┐
   App motorista   WhatsApp     App cidadão / Painel ao vivo
```

- **Google Maps** = distâncias e ordem ótima das paradas.
- **Claude** = quem vai em qual carro, prioridades e exceções — **explicável e editável**.
- **Sempre há um plano**: se a IA falhar, um método automático de reserva garante a operação.

---

## Slide 10 — Equipe do projeto

| Equipe | Quando atua | O que faz |
|--------|-------------|-----------|
| **Desenvolvimento** | Fases 1–3 · 24/06–10/07 | Backend, painel, apps e integrações (Maps, IA, WhatsApp) |
| **Implantação** | Fase 4 · 11–13/07 | Testes, treinamento, piloto assistido, go-live |
| **Suporte** | Após o piloto · contínuo | Sustentação, monitoramento e evolução (Fase 2) |

> Equipes **encadeadas**: desenvolvimento → implantação → suporte, junto à equipe da SMS/OS.

---

## Slide 11 — Riscos e como tratamos

| Risco | Tratamento |
|-------|-----------|
| Conta Meta da Secretaria + verificação (prazo externo) | Inicia no D1; piloto com número de teste se preciso |
| Endereços incompletos | Geocodificação + **pin manual** no mapa |
| Prazo curto de entrega | Equipes dedicadas + reuso da base + IA + **piloto com 1–2 veículos** |
| Sinal de internet no trajeto | App **funciona offline** e sincroniza depois |

---

## Slide 11b — WhatsApp & conta Meta da Secretaria

Para usar o **WhatsApp oficial** (sem risco de bloqueio), é preciso criar uma **conta na
Meta em nome da Secretaria de Saúde de Maricá** e comprovar a habilitação:

- **Contas:** Meta Business + WhatsApp Business Platform + número dedicado.
- **Documentos (SMS):** CNPJ · ato oficial (nome+CNPJ) · comprovante de endereço · site/e-mail `gov.br` · representante.
- **Prazo:** a verificação é da Meta (dias a semanas) → **começa no D1**; piloto com número de teste se necessário.

> A SMS Maricá fornece os dados/documentos; a equipe técnica faz toda a configuração.

---

## Slide 12 — O que pedimos para começar

- ✅ Aprovação do **escopo** e do **cronograma**.
- ✅ **Criar a conta Meta da Secretaria** + **documentos** de habilitação (slide 11b).
- ✅ Acessos/forma de pagamento: **Google Cloud** e **Meta**.
- ✅ **Número de WhatsApp** dedicado ao TFD.
- ✅ **1–2 motoristas + tablets** e um conjunto de **pacientes reais** para o piloto.
- ✅ Confirmação do **início em 24/06** (kickoff).

---

## Slide 13 — Visão de futuro (Fase 2)

Re-otimização em tempo real · app do cidadão completo (avaliação, histórico) · painéis de
desempenho e eficiência · integração com a regulação (SISREG) · **expansão para toda a
frota** do município.

---

## Slide 14 — Encerramento

**Mais pacientes atendidos no horário, menos quilômetros rodados, comunicação clara e
transparência total.**

Transporte TFD que respeita o tempo do paciente e a eficiência do município.

*Dúvidas e ajustes de escopo são bem-vindos antes do kickoff.*
