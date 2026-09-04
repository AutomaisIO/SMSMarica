using System.Reflection;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SMSMais.Core.RoboAtendimento.Runtime;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Robo;

namespace SMSMais.Core.RoboAtendimento.Treinamento;

public interface IRoboBriefingService
{
    /// <summary>Dossiê completo: estrutura (documento) + estado atual do robô (banco).</summary>
    Task<string> MontarAsync(CancellationToken ct = default);

    /// <summary>Só o recorte de um assunto — usado quando o prompt inteiro seria grande demais.</summary>
    Task<string> MontarAssuntoAsync(Guid assuntoId, CancellationToken ct = default);
}

/// <summary>
/// Monta o <b>briefing</b> que o agente treinador lê antes de tocar em qualquer coisa: como a
/// máquina funciona (documento estático <c>docs/robo/briefing-estrutura.md</c>, embarcado no
/// assembly) somado ao estado real de agora (persona global, guardrail, catálogo de comandos e
/// todos os assuntos com seus treinos, condições e comandos).
///
/// A escolha de gerar o estado do BANCO em vez de manter um "prompt que vai crescendo" num
/// arquivo é deliberada: um arquivo que cresce à mão desincroniza do que o robô realmente executa,
/// e aí o treinamento passa a raciocinar sobre um robô que não existe. O que cresce são os
/// treinos no banco — que já são o modelo treinado.
/// </summary>
public sealed class RoboBriefingService(SmsMaisDbContext db) : IRoboBriefingService
{
    private const string RecursoEstrutura = "SMSMais.Core.RoboAtendimento.Treinamento.BriefingEstrutura.md";
    private static string? _estruturaCache;

    /// <summary>O documento de estrutura, embarcado no assembly (é o mesmo arquivo versionado em
    /// <c>docs/robo/briefing-estrutura.md</c> — o csproj o inclui como recurso).</summary>
    public static string Estrutura => _estruturaCache ??= LerEstrutura();

    private static string LerEstrutura()
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(RecursoEstrutura);
        if (s is null)
            return "(documento de estrutura do robô indisponível neste build)";
        using var r = new StreamReader(s, Encoding.UTF8);
        return r.ReadToEnd();
    }

    public async Task<string> MontarAsync(CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Estrutura.TrimEnd()).AppendLine();
        await EstadoAsync(sb, null, ct);
        return sb.ToString();
    }

    public async Task<string> MontarAssuntoAsync(Guid assuntoId, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Estrutura.TrimEnd()).AppendLine();
        await EstadoAsync(sb, assuntoId, ct);
        return sb.ToString();
    }

    private async Task EstadoAsync(StringBuilder sb, Guid? soEsteAssunto, CancellationToken ct)
    {
        sb.AppendLine("---").AppendLine();
        sb.AppendLine("# ESTADO ATUAL DO ROBÔ (lido do banco agora)").AppendLine();
        sb.AppendLine("Tudo abaixo é o que está valendo em produção neste instante. Os `id` são os");
        sb.AppendLine("identificadores reais — use-os nas ferramentas de alteração.").AppendLine();

        var cfg = await db.RoboConfiguracoes.AsNoTracking().FirstOrDefaultAsync(ct);
        sb.AppendLine("## Configuração global").AppendLine();
        sb.AppendLine($"- Robô ligado: **{(cfg?.Ativo == true ? "sim" : "não")}**");
        sb.AppendLine($"- Modelo padrão: `{cfg?.ModeloPadrao ?? "claude-haiku-4-5-20251001"}`");
        sb.AppendLine($"- Motor: `{cfg?.Motor.ToString() ?? nameof(MotorRobo.Assinatura)}`");
        sb.AppendLine($"- Nome de exibição: {cfg?.NomeExibicao ?? "Assistente virtual"}");
        sb.AppendLine($"- Janela do atendente HUMANO: {Janela(cfg?.HoraAtendimentoHumanoInicio, cfg?.HoraAtendimentoHumanoFim)}"
            + $", dias {Dias(cfg?.DiasSemanaAtendimentoHumano)}");
        if (!string.IsNullOrWhiteSpace(cfg?.MensagemHandOff))
            sb.AppendLine($"- Mensagem de hand-off: \"{cfg!.MensagemHandOff!.Trim()}\"");
        if (!string.IsNullOrWhiteSpace(cfg?.MensagemForaHorario))
            sb.AppendLine($"- Mensagem fora de horário: \"{cfg!.MensagemForaHorario!.Trim()}\"");
        sb.AppendLine();
        sb.AppendLine("### Persona global (camada 1 — vale para todos os assuntos)").AppendLine();
        sb.AppendLine("```");
        sb.AppendLine((cfg?.PersonaGlobal ?? RoboConfiguracao.PersonaGlobalPadrao).Trim());
        sb.AppendLine("```").AppendLine();

        sb.AppendLine("### Guardrail (camada 6 — código; NENHUM treino pode contrariar)").AppendLine();
        sb.AppendLine("```");
        sb.AppendLine(RoboGuardrail.Texto.Trim());
        sb.AppendLine("```").AppendLine();

        sb.AppendLine("## Catálogo de comandos (código — a tela só liga/desliga)").AppendLine();
        sb.AppendLine("| Comando | Escrita? | Sempre disponível? | O que faz |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var i in ComandoRoboCatalogo.Itens)
        {
            var sempre = ComandoRoboCatalogo.Base.Contains(i.Comando) ? "sim" : "não";
            sb.AppendLine($"| `{i.Comando}` | {(i.Escrita ? "sim" : "não")} | {sempre} | {i.Descricao} |");
        }
        sb.AppendLine();
        sb.AppendLine("`ResponderCidadao` está sempre presente e é o único canal de saída.").AppendLine();

        var q = db.RoboAssuntos.AsNoTracking().Where(a => a.ExcluidoEm == null);
        if (soEsteAssunto is { } alvo) q = q.Where(a => a.Id == alvo);

        var assuntos = await q
            .OrderBy(a => a.Ordem).ThenBy(a => a.Nome)
            .Select(a => new
            {
                a.Id, a.Nome, a.Descricao, a.Ativo, a.Padrao, a.Ordem, a.Modelo,
                a.InstrucoesPersona, a.LimiarConfianca, a.MaxInteracoesSemResolver,
                a.HorarioInicio, a.HorarioFim, a.DiasSemana,
                EscalonamentoUnidade = a.EscalonamentoUnidade != null ? a.EscalonamentoUnidade.Nome : null,
                Condicoes = a.Condicoes.OrderBy(c => c.Ordem)
                    .Select(c => new { c.Id, c.Tipo, c.Valor, c.Ativo, c.Ordem }).ToList(),
                Treinos = a.Treinos.OrderBy(t => t.Ordem)
                    .Select(t => new { t.Id, t.Tipo, t.Titulo, t.Conteudo, t.Ativo, t.Ordem }).ToList(),
                Comandos = a.Comandos.Where(c => c.Habilitado).Select(c => c.Comando).ToList(),
            })
            .ToListAsync(ct);

        sb.AppendLine(soEsteAssunto is null
            ? $"## Assuntos ({assuntos.Count})"
            : "## Assunto em foco");
        sb.AppendLine();

        if (assuntos.Count == 0)
        {
            sb.AppendLine("_Nenhum assunto cadastrado._").AppendLine();
            return;
        }

        foreach (var a in assuntos)
        {
            sb.AppendLine($"### {a.Nome}");
            sb.AppendLine($"- id: `{a.Id}`");
            sb.AppendLine($"- ativo: {(a.Ativo ? "sim" : "**NÃO**")}"
                + $" | padrão: {(a.Padrao ? "**SIM** (atende quando nada casa)" : "não")}"
                + $" | ordem: {a.Ordem}");
            if (!string.IsNullOrWhiteSpace(a.Descricao)) sb.AppendLine($"- descrição: {a.Descricao}");
            sb.AppendLine($"- modelo: `{a.Modelo ?? "(padrão global)"}`"
                + $" | limiar de confiança: {a.LimiarConfianca:0.00}"
                + $" | teto de interações: {a.MaxInteracoesSemResolver}");
            sb.AppendLine($"- janela do assunto: {Janela(a.HorarioInicio, a.HorarioFim)}, dias {Dias(a.DiasSemana)}");
            sb.AppendLine($"- hand-off vai para: {a.EscalonamentoUnidade ?? "triagem geral"}");
            sb.AppendLine($"- comandos habilitados: {(a.Comandos.Count == 0
                ? "_nenhum além dos de base_"
                : string.Join(", ", a.Comandos.Select(c => $"`{c}`")))}");
            sb.AppendLine();

            sb.AppendLine("**Persona do assunto (camada 3):**").AppendLine();
            sb.AppendLine("```");
            sb.AppendLine(string.IsNullOrWhiteSpace(a.InstrucoesPersona) ? "(vazia)" : a.InstrucoesPersona.Trim());
            sb.AppendLine("```").AppendLine();

            sb.AppendLine($"**Condições de ativação — roteamento ({a.Condicoes.Count}):**").AppendLine();
            if (a.Condicoes.Count == 0)
            {
                sb.AppendLine("_Nenhuma. Este assunto só é usado se for o padrão ou se escolhido explicitamente._");
            }
            else
            {
                sb.AppendLine("| id | tipo | valor | ativa | ordem |");
                sb.AppendLine("|---|---|---|---|---|");
                foreach (var c in a.Condicoes)
                    sb.AppendLine($"| `{c.Id}` | {c.Tipo} | `{Escapar(c.Valor)}` | {(c.Ativo ? "sim" : "não")} | {c.Ordem} |");
            }
            sb.AppendLine();

            var ativos = a.Treinos.Count(t => t.Ativo);
            sb.AppendLine($"**Treinos — regras locais (camada 4): {ativos} ativos de {a.Treinos.Count}:**").AppendLine();
            if (a.Treinos.Count == 0)
            {
                sb.AppendLine("_Nenhum treino. O robô atende este assunto só com a persona._");
            }
            else
            {
                foreach (var t in a.Treinos)
                {
                    var marca = t.Ativo ? string.Empty : " *(INATIVO — não entra no prompt)*";
                    var titulo = string.IsNullOrWhiteSpace(t.Titulo) ? "(sem título)" : t.Titulo!.Trim();
                    sb.AppendLine($"- `{t.Id}` · **{t.Tipo}** · ordem {t.Ordem}{marca} — **{titulo}**: {t.Conteudo.Trim()}");
                }
            }
            sb.AppendLine();
        }
    }

    private static string Janela(TimeOnly? ini, TimeOnly? fim) =>
        ini is { } i && fim is { } f ? $"{i:HH\\:mm}–{f:HH\\:mm}" : "sem restrição de horário";

    private static string Dias(int? mask)
    {
        if (mask is not { } m) return "todos";
        string[] nomes = ["dom", "seg", "ter", "qua", "qui", "sex", "sáb"];
        var dias = Enumerable.Range(0, 7).Where(b => (m & (1 << b)) != 0).Select(b => nomes[b]).ToArray();
        return dias.Length == 0 ? "**nenhum**" : string.Join("/", dias);
    }

    /// <summary>Um valor de condição pode conter crase (regex) e quebraria a tabela markdown.</summary>
    private static string Escapar(string v) => v.Replace("`", "'").Replace("|", "\\|").Replace("\n", " ");
}
