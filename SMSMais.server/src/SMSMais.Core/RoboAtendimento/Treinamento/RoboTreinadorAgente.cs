using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.RoboAtendimento.Runtime;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Robo;

namespace SMSMais.Core.RoboAtendimento.Treinamento;

/// <summary>Caso de teste que o agente escolheu para verificar a correção depois de aplicada.</summary>
public sealed record CasoTesteTreinamento(string Mensagem, IReadOnlyList<(string Papel, string Texto)> Historico);

/// <summary>O que saiu de um ciclo de análise.</summary>
public sealed record ResultadoTreinamento(
    string Parecer,
    string RastroJson,
    bool AbriuPendencia,
    int AlteracoesAplicadas,
    bool Descartado,
    CasoTesteTreinamento? CasoTeste,
    long TokensEntrada,
    long TokensSaida,
    decimal? CustoUsd);

public interface IRoboTreinadorAgente
{
    /// <summary>Roda proposta → adversários → juiz para um item, aplicando o que for seguro. Não
    /// chama <c>SaveChanges</c>: quem orquestra decide a transação.</summary>
    Task<ResultadoTreinamento> AnalisarAsync(RoboTreinamentoItem item, CancellationToken ct);
}

/// <summary>
/// O agente que trata uma crítica. Três fases, deliberadamente separadas:
///
/// <list type="number">
/// <item><b>Proposta</b> — lê o briefing inteiro (estrutura + estado real do robô) e propõe a
/// correção mínima, dizendo em que camada ela cabe;</item>
/// <item><b>Adversários</b> — três leituras independentes e hostis da proposta, em paralelo:
/// conflito com regra existente, efeito no roteamento entre assuntos, e dano ao cidadão. Existem
/// porque uma regra nova quase nunca é errada sozinha: ela erra ao <i>anular</i> ou <i>tornar
/// dúbia</i> uma regra que já estava lá — e isso não aparece para quem acabou de escrevê-la;</item>
/// <item><b>Juiz</b> — decide com a proposta e os ataques na mão: aplica, abre pendência para o
/// humano, ou descarta.</item>
/// </list>
///
/// O agente só encosta em treino e condição do assunto. Persona, horário, comando e guardrail são
/// decisão humana (ou código) e viram pendência — é o que impede o treinamento de virar um caminho
/// paralelo para reescrever o robô inteiro sem ninguém olhando.
/// </summary>
public sealed class RoboTreinadorAgente(
    ClienteAnthropicTreinamento cliente,
    IRoboBriefingService briefing,
    RoboTreinamentoAplicador aplicador,
    SmsMaisDbContext db,
    ILogger<RoboTreinadorAgente> logger) : IRoboTreinadorAgente
{
    /// <summary>Modelo do treinamento. Não é o do atendimento: aqui a tarefa é raciocínio sobre
    /// regras, roda poucas vezes por dia e um erro custa caro.</summary>
    public const string Modelo = "claude-fable-5-1";

    private const int MaxTokensProposta = 8000;
    private const int MaxTokensAdversario = 4000;
    private const int MaxTokensJuiz = 8000;
    private const int MaxIteracoesJuiz = 8;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    public async Task<ResultadoTreinamento> AnalisarAsync(RoboTreinamentoItem item, CancellationToken ct)
    {
        var dossie = await briefing.MontarAsync(ct);
        var caso = await MontarCasoAsync(item, ct);
        long entrada = 0, saida = 0;

        // ---------- fase 1: proposta ----------
        var (proposta, u1) = await ProporAsync(dossie, caso, ct);
        entrada += u1.Entrada; saida += u1.Saida;

        // ---------- fase 2: adversários ----------
        var ataques = new List<(string Persona, JsonElement Veredito)>();
        var propostaTexto = proposta.HasValue ? proposta.Value.GetRawText() : "{}";
        var tarefas = Adversarios.Select(async adv =>
        {
            var (v, u) = await AtacarAsync(dossie, caso, propostaTexto, adv, ct);
            return (adv.Nome, Veredito: v, u.Entrada, u.Saida);
        }).ToArray();

        foreach (var r in await Task.WhenAll(tarefas))
        {
            entrada += r.Entrada; saida += r.Saida;
            if (r.Veredito.HasValue) ataques.Add((r.Nome, r.Veredito.Value));
        }

        // ---------- fase 3: juiz ----------
        var (julgamento, u3) = await JulgarAsync(dossie, caso, propostaTexto, ataques, item, ct);
        entrada += u3.Entrada; saida += u3.Saida;

        var rastro = JsonSerializer.Serialize(new
        {
            modelo = Modelo,
            proposta = proposta.HasValue ? (object)proposta.Value : null,
            adversarios = ataques.Select(a => new { persona = a.Persona, veredito = (object)a.Veredito }).ToArray(),
            juiz = new
            {
                parecer = julgamento.Parecer,
                descartado = julgamento.Descartado,
                alteracoes = julgamento.Aplicadas,
                pendencias = julgamento.Pendencias,
            },
        }, Json);

        return new ResultadoTreinamento(
            julgamento.Parecer,
            rastro,
            julgamento.Pendencias > 0,
            julgamento.Aplicadas,
            julgamento.Descartado,
            LerCasoTeste(proposta) ?? CasoTesteDoOriginal(caso),
            entrada, saida,
            PrecoModeloIa.Calcular(Modelo, entrada, saida));
    }

    // ================= fase 1 =================

    private async Task<(JsonElement? Proposta, (long Entrada, long Saida) Uso)> ProporAsync(
        string dossie, CasoCritica caso, CancellationToken ct)
    {
        var sistema = dossie + "\n\n" + """
            ---

            # SEU PAPEL AGORA: PROPOR A CORREÇÃO

            Você é responsável pelo treinamento deste robô. Um atendente humano criticou uma
            resposta que o robô deu a um cidadão. Sua tarefa é propor a **correção mínima** que
            impeça o robô de repetir aquilo — sem inventar política nova e sem reescrever o que já
            funciona.

            Princípios:

            - **Menor mudança que resolve.** Uma regra nova bem colocada vale mais que cinco.
            - **Ache a camada certa.** Se a crítica é "caiu no assunto errado", o conserto é
              CONDIÇÃO (roteamento), não treino. Se é "ele não devia ter dito isso neste assunto",
              é TREINO. Se é "isso vale para todo assunto", é persona global — e aí você NÃO aplica,
              você aponta.
            - **Escreva a regra como ela vai ser lida.** O conteúdo do treino entra no prompt do
              robô como um item de lista, sem contexto de tela: precisa se sustentar sozinho, na
              voz imperativa, curto e sem ambiguidade.
            - **O guardrail é intocável.** Se a correção exige contrariá-lo ou exige uma ferramenta
              que não existe no catálogo, diga isso — é alteração de código, não treino.
            - **Nunca proponha regra que mande o robô afirmar algo que ele não possa verificar com
              uma ferramenta que tenha.**

            Termine chamando a ferramenta `propor_correcao` exatamente uma vez.
            """;

        var mensagens = new List<object> { new { role = "user", content = caso.Texto } };
        var turno = await cliente.ChamarAsync(
            Modelo, sistema, mensagens, [FerramentaPropor], "high", MaxTokensProposta, ct);

        var chamada = turno.Chamadas.FirstOrDefault(c => c.Nome == "propor_correcao");
        return (chamada?.Argumentos, (turno.TokensEntrada, turno.TokensSaida));
    }

    // ================= fase 2 =================

    private sealed record Adversario(string Nome, string Instrucao);

    private static readonly Adversario[] Adversarios =
    [
        new("conflito_de_regras", """
            Você é o adversário do CONFLITO. Sua única pergunta é: esta proposta **anula**,
            **contradiz** ou **torna dúbia** alguma regra que já existe? Varra, nesta ordem: o
            guardrail, a persona global, a persona do assunto e cada treino ativo do mesmo assunto.
            Procure especificamente: (a) duas regras mandando fazer coisas opostas na mesma
            situação; (b) uma regra nova que abre exceção a uma proibição existente sem dizer que é
            exceção; (c) redundância que só engorda o prompt; (d) regra que só faz sentido no caso
            que gerou a crítica e vira absurda no caso geral. Cite a regra conflitante pelo texto.
            """),
        new("roteamento_e_escopo", """
            Você é o adversário do ROTEAMENTO. Sua pergunta é: esta proposta coloca a regra no
            assunto errado, ou mexe na classificação de um jeito que rouba mensagens de outro
            assunto? Lembre que as condições são avaliadas na ordem dos assuntos (menor primeiro) e
            que a primeira que casa vence — uma palavra-chave genérica num assunto de ordem baixa
            sequestra todos os assuntos abaixo. Verifique também: a regra vale mesmo só para este
            assunto, ou deveria ser global (e então NÃO pode ser aplicada como treino local)? E o
            assunto padrão — que atende quando nada casa — fica coerente depois disso?
            """),
        new("dano_ao_cidadao", """
            Você é o adversário do DANO. Sua pergunta é: seguindo esta proposta ao pé da letra, o
            robô pode causar prejuízo a alguém? Procure: (a) afirmar ou negar algo sem ferramenta
            que comprove — inclusive negar ausência ("você não tem nada agendado"); (b) revelar
            dado de agendamento antes de identidade confirmada; (c) opinar, orientar ou perguntar
            sobre sintoma, dor, gravidade ou conduta clínica; (d) prometer atendente, remarcação,
            agendamento ou canal que não existe; (e) pedir dado pessoal sem ter o que fazer com
            ele; (f) contradizer um atendente humano. Considere o caso limite, não o caso feliz.
            """),
    ];

    private async Task<(JsonElement? Veredito, (long Entrada, long Saida) Uso)> AtacarAsync(
        string dossie, CasoCritica caso, string proposta, Adversario adv, CancellationToken ct)
    {
        var sistema = dossie + "\n\n---\n\n# SEU PAPEL AGORA: ADVERSÁRIO\n\n" + adv.Instrucao + """


            Você NÃO está aqui para melhorar a proposta nem para ser justo com ela. Está aqui para
            mostrar onde ela quebra. Se depois de procurar de verdade você não achar problema,
            diga que aprova — mas só depois de procurar.

            Termine chamando `veredito_adversario` exatamente uma vez.
            """;

        var texto = new StringBuilder()
            .AppendLine(caso.Texto)
            .AppendLine()
            .AppendLine("## PROPOSTA A SER ATACADA")
            .AppendLine()
            .AppendLine("```json")
            .AppendLine(proposta)
            .AppendLine("```")
            .ToString();

        try
        {
            var mensagens = new List<object> { new { role = "user", content = texto } };
            var turno = await cliente.ChamarAsync(
                Modelo, sistema, mensagens, [FerramentaVeredito], "medium", MaxTokensAdversario, ct);
            return (turno.Chamadas.FirstOrDefault(c => c.Nome == "veredito_adversario")?.Argumentos,
                (turno.TokensEntrada, turno.TokensSaida));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Um adversário que falha não pode derrubar o ciclo — mas o juiz precisa saber que a
            // proposta foi menos atacada do que devia.
            logger.LogWarning(ex, "Adversário {Adversario} falhou no treinamento.", adv.Nome);
            return (null, (0, 0));
        }
    }

    // ================= fase 3 =================

    private sealed record Julgamento(string Parecer, int Aplicadas, int Pendencias, bool Descartado);

    private async Task<(Julgamento, (long Entrada, long Saida))> JulgarAsync(
        string dossie, CasoCritica caso, string proposta,
        IReadOnlyList<(string Persona, JsonElement Veredito)> ataques,
        RoboTreinamentoItem item, CancellationToken ct)
    {
        var faltaram = Adversarios.Length - ataques.Count;
        var sistema = dossie + "\n\n" + """
            ---

            # SEU PAPEL AGORA: JUIZ

            Você tem a proposta de correção e os ataques dos adversários. Decida e execute.

            Três saídas possíveis, e só três:

            1. **Aplicar** — a correção cabe em treino e/ou condição do assunto, e sobreviveu aos
               ataques (talvez ajustada por eles). Chame as ferramentas de alteração. Se um ataque
               mostrou conflito real, corrija a redação em vez de ignorá-lo: uma regra nova que
               contradiz outra ativa deixa o robô dúbio, que é pior que o erro original.
            2. **Abrir pendência** — a decisão não é sua. Use `abrir_pendencia` com tipo
               `RegraNegocio` quando depender de uma escolha da Secretaria (mudar persona global ou
               do assunto, horário, limiar, ligar/desligar comando, ou quando duas leituras
               legítimas da crítica levam a regras diferentes), e `AlteracaoCodigo` quando exigir
               guardrail, comando novo ou motor. Faça a pergunta objetiva, com as opções que você
               enxerga. Pendência **para** o ciclo: só o humano destrava.
            3. **Descartar** — a crítica não procede, ou o robô já se comporta como pedido. Explique
               por quê. Não invente mudança para parecer produtivo.

            Você pode combinar 1 e 2 (aplicar o que é claro e perguntar o que não é).

            REGRAS DURAS:
            - Você só altera TREINO e CONDIÇÃO, e só do assunto certo. Qualquer outra coisa é
              pendência.
            - Não desative um treino existente sem dizer qual e por quê.
            - Toda alteração precisa de justificativa que um humano leia daqui a seis meses e
              entenda.

            Termine SEMPRE chamando `concluir`, com o parecer para o humano ler na tela: o que você
            entendeu da crítica, o que os adversários apontaram, o que você fez e o que ficou em
            aberto. Escreva em português claro, sem jargão de IA.
            """;

        var texto = new StringBuilder();
        texto.AppendLine(caso.Texto).AppendLine();
        texto.AppendLine("## PROPOSTA").AppendLine().AppendLine("```json").AppendLine(proposta).AppendLine("```").AppendLine();
        texto.AppendLine("## ATAQUES DOS ADVERSÁRIOS").AppendLine();
        if (ataques.Count == 0)
        {
            texto.AppendLine("_Nenhum adversário respondeu (falha técnica). Ataque você mesmo a "
                + "proposta antes de decidir, e prefira pendência à aplicação em caso de dúvida._");
        }
        else
        {
            foreach (var (persona, v) in ataques)
                texto.AppendLine($"### {persona}").AppendLine("```json").AppendLine(v.GetRawText()).AppendLine("```").AppendLine();
            if (faltaram > 0)
                texto.AppendLine($"_{faltaram} adversário(s) não responderam por falha técnica._").AppendLine();
        }

        var mensagens = new List<object> { new { role = "user", content = texto.ToString() } };
        long entrada = 0, saida = 0;
        var aplicadas = 0;
        var pendencias = 0;
        var parecer = string.Empty;
        var descartado = false;

        for (var i = 0; i < MaxIteracoesJuiz; i++)
        {
            var turno = await cliente.ChamarAsync(
                Modelo, sistema, mensagens, FerramentasJuiz, "high", MaxTokensJuiz, ct);
            entrada += turno.TokensEntrada; saida += turno.TokensSaida;

            if (turno.Chamadas.Count == 0)
            {
                // Sem ferramenta e sem `concluir`: o modelo escreveu prosa. Aceita-se o texto como
                // parecer só se nada foi aplicado; caso contrário é encerramento sem fecho e o
                // ciclo registra o que houve.
                parecer = TextoDoTurno(turno.Conteudo);
                break;
            }

            var resultados = new List<object>(turno.Chamadas.Count);
            var concluiu = false;

            foreach (var c in turno.Chamadas)
            {
                if (c.Nome == "concluir")
                {
                    parecer = Str(c.Argumentos, "parecer") ?? parecer;
                    descartado = Bool(c.Argumentos, "descartado");
                    concluiu = true;
                    resultados.Add(ClienteAnthropicTreinamento.ResultadoFerramenta(c.Id, "Registrado."));
                    continue;
                }

                try
                {
                    var (msg, aplicou, abriu) = await ExecutarAsync(item, c, ct);
                    aplicadas += aplicou;
                    pendencias += abriu;
                    resultados.Add(ClienteAnthropicTreinamento.ResultadoFerramenta(c.Id, msg));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Ferramenta {Ferramenta} falhou no treinamento do item {Item}.",
                        c.Nome, item.Id);
                    resultados.Add(ClienteAnthropicTreinamento.ResultadoFerramenta(
                        c.Id, $"Falhou: {ex.Message}", erro: true));
                }
            }

            if (concluiu) break;

            mensagens.Add(new { role = "assistant", content = turno.Conteudo });
            mensagens.Add(new { role = "user", content = resultados });
        }

        if (string.IsNullOrWhiteSpace(parecer))
            parecer = aplicadas > 0 || pendencias > 0
                ? "O agente executou as mudanças mas encerrou sem parecer. Confira as alterações e pendências abaixo."
                : "O agente encerrou sem parecer e sem mudanças. Vale mandar treinar de novo.";

        return (new Julgamento(parecer.Trim(), aplicadas, pendencias, descartado), (entrada, saida));
    }

    /// <summary>Executa uma ferramenta do juiz. Devolve (mensagem ao modelo, alterações, pendências).</summary>
    private async Task<(string Mensagem, int Aplicadas, int Pendencias)> ExecutarAsync(
        RoboTreinamentoItem item, ChamadaFerramenta c, CancellationToken ct)
    {
        var justificativa = Str(c.Argumentos, "justificativa") ?? "(sem justificativa)";

        switch (c.Nome)
        {
            case "criar_treino":
            {
                var assunto = Uuid(c.Argumentos, "assuntoId") ?? item.RoboAssuntoId
                    ?? throw new InvalidOperationException("Informe assuntoId: o item não tem assunto engajado.");
                var tipo = Enum.TryParse<TipoTreinoRobo>(Str(c.Argumentos, "tipo"), true, out var t)
                    ? t : TipoTreinoRobo.Instrucao;
                var alt = await aplicador.CriarTreinoAsync(item.Id, assunto, tipo,
                    Str(c.Argumentos, "titulo"), Str(c.Argumentos, "conteudo") ?? string.Empty,
                    justificativa, ct);
                return ($"Treino criado (id {alt.AlvoId}).", 1, 0);
            }
            case "atualizar_treino":
            {
                var id = Uuid(c.Argumentos, "treinoId")
                    ?? throw new InvalidOperationException("treinoId é obrigatório.");
                var tipo = Enum.TryParse<TipoTreinoRobo>(Str(c.Argumentos, "tipo"), true, out var t)
                    ? t : (TipoTreinoRobo?)null;
                await aplicador.AtualizarTreinoAsync(item.Id, id, tipo,
                    Str(c.Argumentos, "titulo"), Str(c.Argumentos, "conteudo"), justificativa, ct);
                return ("Treino atualizado.", 1, 0);
            }
            case "desativar_treino":
            {
                var id = Uuid(c.Argumentos, "treinoId")
                    ?? throw new InvalidOperationException("treinoId é obrigatório.");
                await aplicador.DesativarTreinoAsync(item.Id, id, justificativa, ct);
                return ("Treino desativado (sai do prompt; dá para desfazer).", 1, 0);
            }
            case "criar_condicao":
            {
                var assunto = Uuid(c.Argumentos, "assuntoId") ?? item.RoboAssuntoId
                    ?? throw new InvalidOperationException("Informe assuntoId.");
                var tipo = Enum.TryParse<TipoCondicaoRobo>(Str(c.Argumentos, "tipo"), true, out var t)
                    ? t : TipoCondicaoRobo.PalavraChave;
                var alt = await aplicador.CriarCondicaoAsync(item.Id, assunto, tipo,
                    Str(c.Argumentos, "valor") ?? string.Empty, justificativa, ct);
                return ($"Condição criada (id {alt.AlvoId}).", 1, 0);
            }
            case "desativar_condicao":
            {
                var id = Uuid(c.Argumentos, "condicaoId")
                    ?? throw new InvalidOperationException("condicaoId é obrigatório.");
                await aplicador.DesativarCondicaoAsync(item.Id, id, justificativa, ct);
                return ("Condição desativada.", 1, 0);
            }
            case "abrir_pendencia":
            {
                var tipo = Enum.TryParse<TipoPendenciaTreinamento>(Str(c.Argumentos, "tipo"), true, out var t)
                    ? t : TipoPendenciaTreinamento.RegraNegocio;
                var pergunta = Str(c.Argumentos, "pergunta");
                if (string.IsNullOrWhiteSpace(pergunta))
                    throw new InvalidOperationException("pergunta é obrigatória.");

                var opcoes = LerLista(c.Argumentos, "opcoes");
                db.RoboTreinamentoPendencias.Add(new RoboTreinamentoPendencia
                {
                    Id = Guid.CreateVersion7(),
                    RoboTreinamentoItemId = item.Id,
                    Tipo = tipo,
                    Pergunta = pergunta.Trim(),
                    Contexto = Str(c.Argumentos, "contexto"),
                    OpcoesJson = opcoes.Count == 0 ? null : JsonSerializer.Serialize(opcoes, Json),
                    Status = StatusPendenciaTreinamento.Aberta,
                    CriadoEm = DateTime.UtcNow,
                });
                return ("Pendência aberta — o ciclo vai parar até um humano responder.", 0, 1);
            }
            default:
                return ($"Ferramenta desconhecida: {c.Nome}.", 0, 0);
        }
    }

    // ================= o caso =================

    private sealed record CasoCritica(string Texto, string? MensagemCidadao, List<(string Papel, string Texto)> Historico);

    /// <summary>Monta o texto do caso: crítica, observação, o que o robô disse, o diálogo em volta,
    /// o assunto engajado e as pendências já respondidas por humanos.</summary>
    private async Task<CasoCritica> MontarCasoAsync(RoboTreinamentoItem item, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# O CASO").AppendLine();

        var assunto = item.RoboAssuntoId is { } aid
            ? await db.RoboAssuntos.AsNoTracking().Where(a => a.Id == aid)
                .Select(a => new { a.Nome, a.Id }).FirstOrDefaultAsync(ct)
            : null;

        sb.AppendLine(assunto is null
            ? "**Assunto engajado quando a resposta saiu:** nenhum identificado."
            : $"**Assunto engajado quando a resposta saiu:** {assunto.Nome} (`{assunto.Id}`)");
        sb.AppendLine();

        var historico = LerContexto(item.ContextoJson);
        if (historico.Count > 0)
        {
            sb.AppendLine("## Diálogo em volta (o mais recente por último)").AppendLine();
            foreach (var (papel, texto) in historico)
                sb.AppendLine($"- **{papel}**: {texto.Trim()}");
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(item.Trecho))
        {
            sb.AppendLine("## A resposta do robô que foi criticada").AppendLine();
            sb.AppendLine("```").AppendLine(item.Trecho!.Trim()).AppendLine("```").AppendLine();
        }

        sb.AppendLine("## A crítica do atendente").AppendLine();
        sb.AppendLine(item.Critica.Trim()).AppendLine();

        if (!string.IsNullOrWhiteSpace(item.Observacao))
        {
            sb.AppendLine("## Observação adicional de quem mandou treinar").AppendLine();
            sb.AppendLine(item.Observacao!.Trim()).AppendLine();
        }

        var respondidas = await db.RoboTreinamentoPendencias.AsNoTracking()
            .Where(p => p.RoboTreinamentoItemId == item.Id && p.Status != StatusPendenciaTreinamento.Aberta)
            .OrderBy(p => p.CriadoEm)
            .Select(p => new { p.Tipo, p.Pergunta, p.Resposta, p.Status, p.Autorizado })
            .ToListAsync(ct);

        if (respondidas.Count > 0)
        {
            sb.AppendLine("## Decisões que um humano JÁ tomou sobre este item").AppendLine();
            sb.AppendLine("Estas respostas são **vinculantes**: siga-as, não as reabra.").AppendLine();
            foreach (var p in respondidas)
            {
                var autoriz = p.Tipo == TipoPendenciaTreinamento.AlteracaoCodigo
                    ? p.Autorizado == true ? " (alteração de código AUTORIZADA)" : " (alteração de código NÃO autorizada)"
                    : string.Empty;
                sb.AppendLine($"- **{p.Pergunta.Trim()}**");
                sb.AppendLine(p.Status == StatusPendenciaTreinamento.Dispensada
                    ? $"  → dispensada pelo humano{autoriz}. {p.Resposta?.Trim()}"
                    : $"  → {p.Resposta?.Trim()}{autoriz}");
            }
            sb.AppendLine();
        }

        // A última fala do cidadão é o gatilho natural da simulação de verificação.
        var doCidadao = historico.LastOrDefault(h => h.Papel.Equals("cidadao", StringComparison.OrdinalIgnoreCase));
        return new CasoCritica(sb.ToString(), doCidadao.Texto, historico);
    }

    private static List<(string Papel, string Texto)> LerContexto(string? json)
    {
        var lista = new List<(string, string)>();
        if (string.IsNullOrWhiteSpace(json)) return lista;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return lista;
            foreach (var e in doc.RootElement.EnumerateArray())
            {
                var papel = e.TryGetProperty("papel", out var p) ? p.GetString() : null;
                var texto = e.TryGetProperty("texto", out var t) ? t.GetString() : null;
                if (!string.IsNullOrWhiteSpace(papel) && !string.IsNullOrWhiteSpace(texto))
                    lista.Add((papel!, texto!));
            }
        }
        catch (JsonException) { /* contexto corrompido não pode derrubar a análise */ }
        return lista;
    }

    private static CasoTesteTreinamento? LerCasoTeste(JsonElement? proposta)
    {
        if (proposta is not { } p || !p.TryGetProperty("casoTeste", out var ct)
            || ct.ValueKind != JsonValueKind.Object) return null;

        var msg = Str(ct, "mensagem");
        if (string.IsNullOrWhiteSpace(msg)) return null;

        var hist = new List<(string, string)>();
        if (ct.TryGetProperty("historico", out var h) && h.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in h.EnumerateArray())
            {
                var papel = Str(e, "papel");
                var texto = Str(e, "texto");
                if (!string.IsNullOrWhiteSpace(papel) && !string.IsNullOrWhiteSpace(texto))
                    hist.Add((papel!, texto!));
            }
        }
        return new CasoTesteTreinamento(msg!.Trim(), hist);
    }

    /// <summary>Sem caso de teste proposto, ensaia-se a própria fala do cidadão que gerou a
    /// crítica, com o que veio antes dela.</summary>
    private static CasoTesteTreinamento? CasoTesteDoOriginal(CasoCritica caso)
    {
        if (string.IsNullOrWhiteSpace(caso.MensagemCidadao)) return null;
        var idx = caso.Historico.FindLastIndex(h =>
            h.Papel.Equals("cidadao", StringComparison.OrdinalIgnoreCase));
        var antes = idx > 0 ? caso.Historico.Take(idx).ToList() : [];
        return new CasoTesteTreinamento(caso.MensagemCidadao!, antes);
    }

    // ================= ferramentas =================

    private static readonly FerramentaModelo FerramentaPropor = new(
        "propor_correcao",
        "Registra o diagnóstico e a correção proposta para a crítica.",
        new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "diagnostico", "camada", "mudancas" },
            properties = new
            {
                diagnostico = new
                {
                    type = "string",
                    description = "O que de fato deu errado, na sua leitura — a causa, não o sintoma.",
                },
                camada = new
                {
                    type = "string",
                    @enum = new[] { "treino", "condicao", "persona_assunto", "persona_global", "configuracao", "codigo", "nenhuma" },
                    description = "Em que camada a correção cabe. Só 'treino' e 'condicao' podem ser aplicados; o resto vira pendência.",
                },
                mudancas = new
                {
                    type = "array",
                    description = "As mudanças propostas, em ordem. Vazio quando a crítica não procede ou quando a camada exige humano.",
                    items = new
                    {
                        type = "object",
                        additionalProperties = false,
                        required = new[] { "acao", "justificativa" },
                        properties = new
                        {
                            acao = new
                            {
                                type = "string",
                                @enum = new[] { "criar_treino", "atualizar_treino", "desativar_treino", "criar_condicao", "desativar_condicao" },
                            },
                            assuntoId = new { type = "string", description = "Assunto alvo (uuid)." },
                            alvoId = new { type = "string", description = "Treino ou condição existente (uuid), quando a ação atualiza ou desativa." },
                            tipo = new { type = "string", description = "Instrucao|Exemplo|Glossario|Do|Dont, ou PalavraChave|Regex|Frase." },
                            titulo = new { type = "string" },
                            conteudo = new { type = "string", description = "Texto do treino, como o robô vai lê-lo." },
                            valor = new { type = "string", description = "Valor da condição." },
                            justificativa = new { type = "string" },
                        },
                    },
                },
                casoTeste = new
                {
                    type = "object",
                    additionalProperties = false,
                    description = "Um turno para ensaiar a correção depois de aplicada. Prefira reproduzir o caso real.",
                    properties = new
                    {
                        mensagem = new { type = "string", description = "A mensagem do cidadão." },
                        historico = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "object",
                                additionalProperties = false,
                                properties = new
                                {
                                    papel = new { type = "string", description = "cidadao | robo | atendente | sistema" },
                                    texto = new { type = "string" },
                                },
                            },
                        },
                    },
                },
                observacoes = new { type = "string" },
            },
        });

    private static readonly FerramentaModelo FerramentaVeredito = new(
        "veredito_adversario",
        "Registra o ataque à proposta sob a sua ótica.",
        new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "veredito", "conflitos", "recomendacao" },
            properties = new
            {
                veredito = new
                {
                    type = "string",
                    @enum = new[] { "aprova", "ressalva", "rejeita" },
                },
                conflitos = new
                {
                    type = "array",
                    description = "Cada problema encontrado. Vazio se aprovou.",
                    items = new
                    {
                        type = "object",
                        additionalProperties = false,
                        required = new[] { "tipo", "explicacao", "gravidade" },
                        properties = new
                        {
                            tipo = new
                            {
                                type = "string",
                                @enum = new[] { "anula", "contradiz", "ambiguidade", "escopo", "redundancia", "dano" },
                            },
                            regra = new { type = "string", description = "A regra existente afetada, citada pelo texto." },
                            explicacao = new { type = "string" },
                            gravidade = new { type = "string", @enum = new[] { "baixa", "media", "alta" } },
                        },
                    },
                },
                recomendacao = new
                {
                    type = "string",
                    description = "O que fazer: aplicar como está, ajustar (dizendo como), ou levar ao humano.",
                },
            },
        });

    private static readonly FerramentaModelo[] FerramentasJuiz =
    [
        new("criar_treino", "Cria uma regra local (treino) no assunto.", new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "conteudo", "justificativa" },
            properties = new
            {
                assuntoId = new { type = "string", description = "uuid; omitido usa o assunto engajado no caso." },
                tipo = new { type = "string", @enum = new[] { "Instrucao", "Exemplo", "Glossario", "Do", "Dont" } },
                titulo = new { type = "string" },
                conteudo = new { type = "string" },
                justificativa = new { type = "string" },
            },
        }),
        new("atualizar_treino", "Reescreve um treino existente.", new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "treinoId", "justificativa" },
            properties = new
            {
                treinoId = new { type = "string" },
                tipo = new { type = "string", @enum = new[] { "Instrucao", "Exemplo", "Glossario", "Do", "Dont" } },
                titulo = new { type = "string" },
                conteudo = new { type = "string" },
                justificativa = new { type = "string" },
            },
        }),
        new("desativar_treino", "Tira um treino do prompt (reversível).", new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "treinoId", "justificativa" },
            properties = new
            {
                treinoId = new { type = "string" },
                justificativa = new { type = "string" },
            },
        }),
        new("criar_condicao", "Cria uma condição de ativação (roteamento) no assunto.", new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "valor", "justificativa" },
            properties = new
            {
                assuntoId = new { type = "string" },
                tipo = new { type = "string", @enum = new[] { "PalavraChave", "Regex", "Frase" } },
                valor = new { type = "string" },
                justificativa = new { type = "string" },
            },
        }),
        new("desativar_condicao", "Desativa uma condição de ativação.", new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "condicaoId", "justificativa" },
            properties = new
            {
                condicaoId = new { type = "string" },
                justificativa = new { type = "string" },
            },
        }),
        new("abrir_pendencia", "Trava o ciclo e pergunta ao humano.", new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "tipo", "pergunta" },
            properties = new
            {
                tipo = new { type = "string", @enum = new[] { "RegraNegocio", "AlteracaoCodigo" } },
                pergunta = new { type = "string" },
                contexto = new { type = "string", description = "Por que você precisa disso." },
                opcoes = new { type = "array", items = new { type = "string" } },
            },
        }),
        new("concluir", "Fecha a análise com o parecer para o humano.", new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "parecer" },
            properties = new
            {
                parecer = new { type = "string" },
                descartado = new { type = "boolean", description = "true quando a crítica não procede e nada foi mudado." },
            },
        }),
    ];

    // ================= leitura de JSON =================

    private static string? Str(JsonElement o, string campo) =>
        o.ValueKind == JsonValueKind.Object && o.TryGetProperty(campo, out var v)
            && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool Bool(JsonElement o, string campo) =>
        o.ValueKind == JsonValueKind.Object && o.TryGetProperty(campo, out var v)
            && v.ValueKind == JsonValueKind.True;

    private static Guid? Uuid(JsonElement o, string campo) =>
        Guid.TryParse(Str(o, campo), out var g) ? g : null;

    private static List<string> LerLista(JsonElement o, string campo)
    {
        var lista = new List<string>();
        if (o.ValueKind != JsonValueKind.Object || !o.TryGetProperty(campo, out var v)
            || v.ValueKind != JsonValueKind.Array) return lista;
        foreach (var e in v.EnumerateArray())
            if (e.ValueKind == JsonValueKind.String && e.GetString() is { Length: > 0 } s) lista.Add(s);
        return lista;
    }

    private static string TextoDoTurno(JsonElement conteudo)
    {
        if (conteudo.ValueKind != JsonValueKind.Array) return string.Empty;
        var sb = new StringBuilder();
        foreach (var b in conteudo.EnumerateArray())
        {
            if (b.TryGetProperty("type", out var t) && t.GetString() == "text"
                && b.TryGetProperty("text", out var x) && x.GetString() is { Length: > 0 } s)
                sb.AppendLine(s);
        }
        return sb.ToString().Trim();
    }
}
