using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMarica.Core.Cidadao;
using SMSMarica.Core.Common.Tempo;
using SMSMarica.Core.Conversas;
using SMSMarica.Core.Notificacoes.WhatsApp;
using SMSMarica.Core.Pacientes;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Notificacoes.Comunicacao;

/// <summary>
/// Fila de comunicações ao paciente. Os gatilhos (import, Realizada, laudo assinado) só
/// ENFILEIRAM (<see cref="EnfileirarAsync"/>); o worker (<see cref="EnviadorComunicacaoService"/>)
/// processa com ritmo e retentativas: valida celular BR, gera magic link com destino por
/// finalidade e envia o template correspondente.
/// </summary>
public interface IComunicacaoPacienteService
{
    /// <summary>Enfileira a comunicação (idempotente por solicitação × finalidade). Para
    /// ConfirmacaoAgendamento exige DataAgendada futura (senão não faz nada). NÃO salva —
    /// participa do SaveChanges do chamador.</summary>
    Task EnfileirarAsync(SolicitacaoExame solicitacao, FinalidadeComunicacao finalidade, CancellationToken ct = default);

    /// <summary>Processa UMA tentativa de envio. Nunca lança — falha vira backoff/estado terminal.</summary>
    Task ProcessarTentativaEnvioAsync(Guid comunicacaoId, CancellationToken ct = default);
}

public sealed class ComunicacaoPacienteService(
    SmsMaricaDbContext db,
    IPacientesService pacientes,
    ICidadaoLoginLinkService loginLinks,
    IWhatsAppCliente whatsApp,
    IOptions<ComunicacaoPacienteOptions> options,
    ILogger<ComunicacaoPacienteService> logger) : IComunicacaoPacienteService
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Códigos de erro da Meta que não adianta retentar (número inexistente/não autorizado).</summary>
    private static readonly string[] ErrosMetaPermanentes = ["131026", "131030"];

    public async Task EnfileirarAsync(
        SolicitacaoExame solicitacao, FinalidadeComunicacao finalidade, CancellationToken ct = default)
    {
        // Confirmação só faz sentido antes do exame; as demais finalidades valem sempre.
        if (finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento
            && (solicitacao.DataAgendada is not { } da || da <= DateTime.UtcNow))
            return;

        // Idempotência: uma comunicação por solicitação × finalidade (o índice único garante;
        // a checagem evita a exceção quando o gatilho re-dispara).
        var jaExiste = await db.ComunicacoesPaciente
            .AnyAsync(c => c.SolicitacaoExameId == solicitacao.Id && c.Finalidade == finalidade, ct);
        if (jaExiste) return;

        db.ComunicacoesPaciente.Add(new ComunicacaoPaciente
        {
            Id = Guid.CreateVersion7(),
            Tipo = TipoAgendamento.Exame,
            Finalidade = finalidade,
            SolicitacaoExameId = solicitacao.Id,
            PacienteId = solicitacao.PacienteId,
            Status = StatusComunicacao.Pendente,
            ProximaTentativaEm = DateTime.UtcNow,
            CriadoEm = DateTime.UtcNow,
        });
    }

    public async Task ProcessarTentativaEnvioAsync(Guid comunicacaoId, CancellationToken ct = default)
    {
        var n = await db.ComunicacoesPaciente
            .Include(x => x.SolicitacaoExame!).ThenInclude(s => s.TipoExame)
            .Include(x => x.SolicitacaoExame!).ThenInclude(s => s.Unidade)
            .FirstOrDefaultAsync(x => x.Id == comunicacaoId, ct);
        if (n is null || n.Status != StatusComunicacao.Pendente || n.ProximaTentativaEm is null)
            return;

        n.Tentativas++;
        n.UltimaTentativaEm = DateTime.UtcNow;
        n.AtualizadoEm = DateTime.UtcNow;

        try
        {
            var s = n.SolicitacaoExame;

            if (s is null || s.ExcluidoEm is not null || s.Status == StatusSolicitacaoExame.Cancelada)
            {
                Terminal(n, StatusComunicacao.Falha, "Solicitação excluída ou cancelada antes do envio.");
            }
            else if (n.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento
                     && (s.DataAgendada is not { } dataAgendada || dataAgendada <= DateTime.UtcNow))
            {
                Terminal(n, StatusComunicacao.Falha, "Exame sem data futura no momento do envio.");
            }
            else if (n.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento
                     && s.StatusConfirmacao != StatusConfirmacaoAgendamento.Pendente)
            {
                Terminal(n, StatusComunicacao.Falha, "Paciente já respondeu por outro canal.");
            }
            else
            {
                await EnviarAsync(n, s, ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao enviar comunicação {Id} ({Finalidade}, tentativa {N}).",
                n.Id, n.Finalidade, n.Tentativas);
            ReagendarOuFalhar(n, ex.Message);
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task EnviarAsync(ComunicacaoPaciente n, SolicitacaoExame s, CancellationToken ct)
    {
        var paciente = await pacientes.ObterPorIdAsync(n.PacienteId, ct);
        var cpf = SoDigitos(paciente.Cpf);

        // Telefone: contato validado por OTP (âncora por CPF) > celular do cadastro > principal.
        string? telefone = null;
        if (cpf.Length == 11)
            telefone = await db.ContatosValidados.AsNoTracking()
                .Where(c => c.Cpf == cpf).Select(c => c.Numero).FirstOrDefaultAsync(ct);
        telefone ??= new[] { paciente.TelefoneCelular, paciente.TelefonePrincipal }
            .FirstOrDefault(TelefoneWhatsApp.EhCelularBr);

        if (!TelefoneWhatsApp.EhCelularBr(telefone))
        {
            Terminal(n, StatusComunicacao.SemTelefoneValido, "Paciente sem número de celular válido.");
            return;
        }
        n.Telefone = TelefoneWhatsApp.Canonizar(telefone!);

        // Magic link novo a cada tentativa (o anterior simplesmente expira sem uso).
        var link = await loginLinks.GerarParaSolicitacaoAsync(s.Id, Destino(n.Finalidade, s.Id), ct);
        n.LoginLinkId = link.Token;

        var opts = options.Value;
        var (template, parametros, botoes) = MontarEnvio(n.Finalidade, n.Tipo, s, paciente.NomeCompleto, link.Token, opts);

        var resultado = await whatsApp.EnviarTemplateComBotoesAsync(
            n.Telefone, template, opts.Idioma, parametros, botoes, pacienteId: n.PacienteId, ct: ct);

        if (resultado.Ok)
        {
            n.Status = StatusComunicacao.Enviada;
            n.EnviadoEm = DateTime.UtcNow;
            n.MotivoFalha = null;
            n.ProximaTentativaEm = null; // recibos (entrega/leitura/falha) chegam pelo webhook
            if (resultado.WaMessageId is { } wamid)
                n.MensagemWhatsAppId = await db.MensagensWhatsApp.AsNoTracking()
                    .Where(m => m.WaMessageId == wamid).Select(m => (Guid?)m.Id).FirstOrDefaultAsync(ct);
        }
        else if (ErrosMetaPermanentes.Any(c => resultado.Erro?.Contains($"({c})") == true))
        {
            Terminal(n, StatusComunicacao.Falha, resultado.Erro);
        }
        else
        {
            ReagendarOuFalhar(n, resultado.Erro);
        }
    }

    /// <summary>Rota de chegada no app após o magic link, por finalidade. Exame liberado e
    /// laudo pronto caem em /exames com o CARD do exame já expandido (?exame={id}).</summary>
    private static string Destino(FinalidadeComunicacao finalidade, Guid solicitacaoId) => finalidade switch
    {
        FinalidadeComunicacao.ExameLiberado or FinalidadeComunicacao.LaudoPronto
            => $"/exames?exame={solicitacaoId}",
        _ => "/agendados/exames",
    };

    private static (string Template, string[] Parametros, BotaoTemplateWhatsApp[] Botoes) MontarEnvio(
        FinalidadeComunicacao finalidade, TipoAgendamento tipo, SolicitacaoExame s, string? nomePaciente,
        Guid token, ComunicacaoPacienteOptions opts)
    {
        var nome = PrimeiroNome(nomePaciente);
        var exame = s.TipoExame?.Nome ?? "exame";
        var url = new BotaoTemplateWhatsApp(TipoBotaoTemplate.Url, token.ToString());

        switch (finalidade)
        {
            // exame_liberado / laudo_disponivel: "seu exame de {{2}}, realizado {{3}}, está pronto…"
            case FinalidadeComunicacao.ExameLiberado:
                return (opts.TemplateExameLiberado, [nome, exame, DataRealizacao(s)], [url]);

            case FinalidadeComunicacao.LaudoPronto:
                return (opts.TemplateLaudoPronto, [nome, exame, DataRealizacao(s)], [url]);

            // confirmar_agendamento_urlapp (ÚNICO template de confirmação — serve exame E
            // consulta; confirma_exame/confirma_consulta foram descontinuados na Meta):
            // "você tem {{2}} de *{{3}}* agendado para o dia *{{4}}*, {{5}}📍, às *{{6}}*.
            // Endereço: {{7}}". Botões na ordem do template: URL (index 0) + quick reply (index 1).
            default:
                var local = FusoBrasilia.ParaExibicao(s.DataAgendada!.Value);
                return (
                    opts.TemplateConfirmaAgendamento,
                    [
                        nome,
                        tipo == TipoAgendamento.Consulta ? "uma consulta" : "um exame",
                        exame,
                        local.ToString("dd/MM/yyyy", PtBr),
                        $"na unidade {s.Unidade?.Nome ?? "de saúde"}",
                        $"{local.ToString("HH:mm", PtBr)}h",
                        EnderecoUnidade(s.Unidade),
                    ],
                    [url, new BotaoTemplateWhatsApp(TipoBotaoTemplate.QuickReply, $"confirma:{s.Id}")]);
        }
    }

    /// <summary>Data em que o exame foi feito (DICOM → detecção → criação), formatada dd/MM/aaaa.</summary>
    private static string DataRealizacao(SolicitacaoExame s)
    {
        // DataEstudo é wall-clock do equipamento (as-is); os demais são UTC → Brasília.
        var d = s.DataEstudo
            ?? (s.RealizadoEm is { } r ? FusoBrasilia.ParaExibicao(r) : FusoBrasilia.ParaExibicao(s.CriadoEm));
        return d.ToString("dd/MM/yyyy", PtBr);
    }

    /// <summary>Endereço da unidade para o template (a Meta rejeita parâmetro vazio).</summary>
    private static string EnderecoUnidade(Unidade? u)
    {
        var e = u?.Endereco;
        if (e is null) return "Maricá/RJ";
        var partes = new[]
        {
            string.Join(", ", new[] { e.Logradouro, e.Numero }.Where(p => !string.IsNullOrWhiteSpace(p))),
            e.Bairro,
        }.Where(p => !string.IsNullOrWhiteSpace(p));
        var texto = string.Join(" - ", partes);
        return string.IsNullOrWhiteSpace(texto) ? "Maricá/RJ" : texto;
    }

    private void ReagendarOuFalhar(ComunicacaoPaciente n, string? erro)
    {
        n.MotivoFalha = Truncar(erro);
        if (n.Tentativas >= Math.Max(1, options.Value.MaxTentativas))
        {
            n.Status = StatusComunicacao.Falha;
            n.ProximaTentativaEm = null;
            return;
        }
        // Backoff exponencial: 5min, 10min, 20min, 40min...
        n.ProximaTentativaEm = DateTime.UtcNow.AddMinutes(5 * Math.Pow(2, n.Tentativas - 1));
    }

    private static void Terminal(ComunicacaoPaciente n, StatusComunicacao status, string? motivo)
    {
        n.Status = status;
        n.MotivoFalha = Truncar(motivo);
        n.ProximaTentativaEm = null;
    }

    private static string PrimeiroNome(string? nome)
    {
        var partes = (nome ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return partes.Length == 0 ? "Paciente" : PtBr.TextInfo.ToTitleCase(partes[0].ToLowerInvariant());
    }

    private static string SoDigitos(string? v) =>
        string.IsNullOrEmpty(v) ? string.Empty : new string([.. v.Where(char.IsDigit)]);

    private static string? Truncar(string? s) => s is null ? null : s.Length <= 1000 ? s : s[..1000];
}
