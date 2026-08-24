using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Conversas;
using SMSMais.Core.Identidade;
using SMSMais.Core.Notificacoes.WhatsApp;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Core.Sandbox.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Sandbox;

/// <summary>
/// Sandbox de QA (admin): opera sobre um paciente REAL escolhido pelo operador para testar a
/// dinâmica do magic link, a visualização no PWA (exames/laudos/agendados, tudo real) e o fluxo
/// de confirmação. Não fabrica dados clínicos — só (a) gera magic link, (b) envia mensagem de
/// texto livre (funciona na janela de 24h, sem template), e (c) força o estado de confirmação de
/// uma solicitação, de forma reversível. Tudo gateado por <see cref="ModuloPermissao.Sandbox"/>.
/// </summary>
public interface ISandboxService
{
    Task<IReadOnlyList<SandboxPacienteDto>> BuscarPacientesAsync(string termo, CancellationToken ct = default);
    Task<IReadOnlyList<SandboxSolicitacaoDto>> ListarSolicitacoesAsync(Guid pacienteId, CancellationToken ct = default);
    Task<LinkTesteDto> GerarLinkAsync(GerarLinkRequest req, CancellationToken ct = default);
    Task<ResultadoEnvioTesteDto> EnviarMensagemAsync(EnviarMensagemTesteRequest req, CancellationToken ct = default);
    Task DefinirConfirmacaoAsync(DefinirConfirmacaoRequest req, CancellationToken ct = default);

    /// <summary>Simula o ciclo dos checks (✓/✓✓/✓✓ azul/⚠) de uma comunicação, sem Meta.</summary>
    Task SimularComunicacaoAsync(SimularComunicacaoRequest req, CancellationToken ct = default);
}

public sealed class SandboxService(
    SmsMaisDbContext db,
    IPacienteResolver pacientes,
    IWhatsAppCliente whatsApp,
    IUsuarioAtualAccessor usuarioAtual,
    IConfiguration configuration) : ISandboxService
{
    public async Task<IReadOnlyList<SandboxPacienteDto>> BuscarPacientesAsync(string termo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(termo) || termo.Trim().Length < 3) return [];
        var ids = await pacientes.BuscarIdsPorTermoAsync(termo.Trim(), ct: ct);
        if (ids.Count == 0) return [];
        var mapa = await pacientes.ResolverManyAsync(ids.Take(20), ct);
        return [.. mapa.Values
            .OrderBy(p => p.Nome)
            .Select(p => new SandboxPacienteDto(p.Id, p.Nome, p.Cpf))];
    }

    public async Task<IReadOnlyList<SandboxSolicitacaoDto>> ListarSolicitacoesAsync(
        Guid pacienteId, CancellationToken ct = default)
    {
        return await db.ExamesImagem.AsNoTracking()
            .Where(s => s.Solicitacao!.PacienteId == pacienteId && s.ExcluidoEm == null)
            .OrderByDescending(s => s.Solicitacao!.DataAgendada ?? s.CriadoEm)
            .Take(50)
            .Select(s => new SandboxSolicitacaoDto(
                s.Id,
                s.AccessionNumber,
                s.TipoExame != null ? s.TipoExame.Nome : null,
                s.Solicitacao!.DataAgendada,
                s.Status.ToString(),
                s.Solicitacao!.StatusConfirmacao.ToString()))
            .ToListAsync(ct);
    }

    public async Task<LinkTesteDto> GerarLinkAsync(GerarLinkRequest req, CancellationToken ct = default)
    {
        var (cpf, _) = await ObterCpfAsync(req.PacienteId, ct);
        var link = CriarLoginLink(req.PacienteId, cpf, req.Destino);
        db.CidadaoLoginLinks.Add(link);
        await db.SaveChangesAsync(ct);
        return new LinkTesteDto(MontarUrl(link.Id), link.ExpiraEm);
    }

    public async Task<ResultadoEnvioTesteDto> EnviarMensagemAsync(
        EnviarMensagemTesteRequest req, CancellationToken ct = default)
    {
        var telefone = req.Telefone?.Trim();
        if (string.IsNullOrWhiteSpace(telefone))
            throw new ValidacaoException("sandbox.telefone", "Informe o telefone de destino.");

        string? url = null;
        var texto = req.Texto ?? string.Empty;

        if (req.PacienteId is { } pid)
        {
            var (cpf, _) = await ObterCpfAsync(pid, ct);
            var link = CriarLoginLink(pid, cpf, req.Destino);
            db.CidadaoLoginLinks.Add(link);
            await db.SaveChangesAsync(ct);
            url = MontarUrl(link.Id);
            texto = string.IsNullOrWhiteSpace(texto) ? url : $"{texto}\n\n{url}";
        }

        // Texto livre: só entrega dentro da janela de 24h (abra mandando uma msg ao número).
        var r = await whatsApp.EnviarTextoAsync(TelefoneWhatsApp.Canonizar(telefone), texto, pacienteId: req.PacienteId, ct: ct);
        return new ResultadoEnvioTesteDto(r.Ok, r.Erro, url);
    }

    public async Task DefinirConfirmacaoAsync(DefinirConfirmacaoRequest req, CancellationToken ct = default)
    {
        // req.SolicitacaoExameId é o id público (exame) → traduz para a espinha (StatusConfirmacao é regulação).
        var solicitacaoId = await db.ExamesImagem.AsNoTracking()
            .Where(e => e.Id == req.SolicitacaoExameId).Select(e => (Guid?)e.SolicitacaoId).FirstOrDefaultAsync(ct)
            ?? req.SolicitacaoExameId;
        var s = await db.Solicitacoes.FirstOrDefaultAsync(
            x => x.Id == solicitacaoId && x.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException("sandbox.solicitacao", "Solicitação não encontrada.");

        switch (req.Estado?.Trim().ToLowerInvariant())
        {
            case "pendente":
                s.StatusConfirmacao = StatusConfirmacaoAgendamento.Pendente;
                s.ConfirmadoEm = null;
                s.ConfirmadoCanal = null;
                s.ConfirmacaoCanceladaEm = null;
                s.MotivoCancelamentoPaciente = null;
                break;
            case "confirmada":
                s.StatusConfirmacao = StatusConfirmacaoAgendamento.Confirmada;
                s.ConfirmadoEm = DateTime.UtcNow;
                s.ConfirmadoCanal = "sandbox";
                break;
            case "cancelada":
                s.StatusConfirmacao = StatusConfirmacaoAgendamento.Cancelada;
                s.ConfirmacaoCanceladaEm = DateTime.UtcNow;
                s.ConfirmadoCanal = "sandbox";
                s.MotivoCancelamentoPaciente = string.IsNullOrWhiteSpace(req.Motivo) ? "Teste (sandbox)" : req.Motivo.Trim();
                break;
            default:
                throw new ValidacaoException("sandbox.estado", "Estado inválido (use pendente | confirmada | cancelada).");
        }
        s.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task SimularComunicacaoAsync(SimularComunicacaoRequest req, CancellationToken ct = default)
    {
        if (!Enum.TryParse<FinalidadeComunicacao>(req.Finalidade, ignoreCase: true, out var finalidade))
            throw new ValidacaoException("sandbox.finalidade",
                "Finalidade inválida (ConfirmacaoAgendamento|ExameLiberado|LaudoPronto).");

        var s = await db.ExamesImagem.AsNoTracking()
            .Where(x => x.Id == req.SolicitacaoExameId && x.ExcluidoEm == null)
            .Select(x => new { x.SolicitacaoId, x.Solicitacao!.PacienteId })
            .FirstOrDefaultAsync(ct)
            ?? throw new NaoEncontradoException("sandbox.solicitacao", "Solicitação não encontrada.");

        // Upsert por (solicitação, finalidade) — respeita o índice único. Comunicação é ancorada na espinha.
        var c = await db.ComunicacoesPaciente.FirstOrDefaultAsync(
            x => x.SolicitacaoId == s.SolicitacaoId && x.Finalidade == finalidade, ct);
        if (c is null)
        {
            c = new ComunicacaoPaciente
            {
                Id = Guid.CreateVersion7(),
                Tipo = TipoAgendamento.Exame,
                Finalidade = finalidade,
                SolicitacaoId = s.SolicitacaoId,
                PacienteId = s.PacienteId,
                CriadoEm = DateTime.UtcNow,
            };
            db.ComunicacoesPaciente.Add(c);
        }

        var agora = DateTime.UtcNow;
        c.AtualizadoEm = agora;
        c.ProximaTentativaEm = null; // simulado — o worker não deve pegar
        switch (req.Estado?.Trim().ToLowerInvariant())
        {
            case "enviada":
                c.Status = StatusComunicacao.Enviada;
                c.Telefone ??= "5521999990000";
                c.Tentativas = Math.Max(1, c.Tentativas);
                c.EnviadoEm = agora;
                c.EntregueEm = null; c.LidoEm = null; c.VisualizadoEm = null; c.MotivoFalha = null;
                break;
            case "entregue":
                c.Status = StatusComunicacao.Entregue;
                c.EnviadoEm ??= agora;
                c.EntregueEm = agora;
                c.MotivoFalha = null;
                break;
            case "lida":
                c.Status = StatusComunicacao.Lida;
                c.EnviadoEm ??= agora;
                c.EntregueEm ??= agora;
                c.LidoEm = agora;
                c.MotivoFalha = null;
                break;
            case "visualizada":
                c.EnviadoEm ??= agora;
                c.VisualizadoEm = agora;
                break;
            case "falha":
                c.Status = StatusComunicacao.Falha;
                c.MotivoFalha = "(SIMULADO) Falha de entrega gerada pelo sandbox.";
                break;
            case "reset":
                c.Status = StatusComunicacao.Pendente;
                c.EnviadoEm = null; c.EntregueEm = null; c.LidoEm = null; c.VisualizadoEm = null;
                c.MotivoFalha = null; c.Tentativas = 0;
                break;
            default:
                throw new ValidacaoException("sandbox.estado",
                    "Estado inválido (enviada|entregue|lida|visualizada|falha|reset).");
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task<(string Cpf, string Nome)> ObterCpfAsync(Guid pacienteId, CancellationToken ct)
    {
        var p = await pacientes.ResolverAsync(pacienteId, ct)
            ?? throw new NaoEncontradoException("sandbox.paciente", "Paciente não encontrado no hub.");
        var cpf = new string([.. (p.Cpf ?? "").Where(char.IsDigit)]);
        if (cpf.Length != 11)
            throw new ConflitoException("sandbox.sem_cpf", "Paciente sem CPF válido para gerar o link.");
        return (cpf, p.Nome);
    }

    private CidadaoLoginLink CriarLoginLink(Guid patientId, string cpf, string? destino) => new()
    {
        Id = Guid.CreateVersion7(),
        PatientId = patientId,
        Cpf = cpf,
        Destino = string.IsNullOrWhiteSpace(destino) ? "/agendados/exames" : destino,
        ExpiraEm = DateTime.UtcNow.AddDays(7),
        CriadoEm = DateTime.UtcNow,
        CriadoPor = usuarioAtual.UsuarioId,
    };

    private string MontarUrl(Guid token)
    {
        var appBase = (configuration["Publico:AppBaseUrl"] ?? "https://app.smsmarica.online").TrimEnd('/');
        return $"{appBase}/entrar/{token}";
    }
}
