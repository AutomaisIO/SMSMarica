using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.SolicitacoesExame.Dtos;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.SolicitacoesExame;

/// <summary>
/// Histórico do processo de comunicação de uma solicitação (linha do tempo do detalhe):
/// comunicações automáticas (WhatsApp, com recibos/visualização) + contatos MANUAIS
/// registrados pela equipe ("liguei, não atendeu"...). O registro de contato é append-only.
/// </summary>
public interface ISolicitacaoHistoricoService
{
    Task<HistoricoSolicitacaoDto> ObterAsync(Guid solicitacaoExameId, CancellationToken ct = default);
    Task RegistrarContatoAsync(Guid solicitacaoExameId, RegistrarContatoRequest request, CancellationToken ct = default);
}

public sealed class SolicitacaoHistoricoService(
    SmsMaricaDbContext db,
    IUsuarioAtualAccessor usuarioAtual) : ISolicitacaoHistoricoService
{
    public async Task<HistoricoSolicitacaoDto> ObterAsync(Guid solicitacaoExameId, CancellationToken ct = default)
    {
        // Comunicações/contatos são ancorados na espinha; traduz o id público (exame) → espinha.
        var solicitacaoId = await db.ExamesImagem.AsNoTracking()
            .Where(e => e.Id == solicitacaoExameId).Select(e => (Guid?)e.SolicitacaoId).FirstOrDefaultAsync(ct)
            ?? solicitacaoExameId;

        var comunicacoes = await db.ComunicacoesPaciente.AsNoTracking()
            .Where(c => c.SolicitacaoId == solicitacaoId)
            .OrderBy(c => c.CriadoEm)
            .Select(c => new HistoricoComunicacaoDto(
                c.Id,
                c.Finalidade.ToString(),
                c.Status.ToString(),
                c.Telefone,
                c.Tentativas,
                c.CriadoEm,
                c.EnviadoEm,
                c.EntregueEm,
                c.LidoEm,
                c.VisualizadoEm,
                c.MotivoFalha,
                c.MensagemWhatsApp != null ? c.MensagemWhatsApp.ErroMeta : null))
            .ToListAsync(ct);

        var contatos = await (
            from c in db.ContatosRegistro.AsNoTracking()
            where c.SolicitacaoId == solicitacaoId
            join u in db.Usuarios.AsNoTracking() on c.CriadoPor equals u.Id into ju
            from u in ju.DefaultIfEmpty()
            orderby c.CriadoEm descending
            select new HistoricoContatoDto(
                c.Id,
                c.Meio.ToString(),
                c.Resultado.ToString(),
                c.Observacao,
                c.CriadoEm,
                u != null ? u.NomeCompleto : null))
            .ToListAsync(ct);

        return new HistoricoSolicitacaoDto(comunicacoes, contatos);
    }

    public async Task RegistrarContatoAsync(
        Guid solicitacaoExameId, RegistrarContatoRequest request, CancellationToken ct = default)
    {
        if (!Enum.TryParse<MeioContato>(request.Meio, ignoreCase: true, out var meio))
            throw new ValidacaoException("contato.meio_invalido", "Meio inválido (Ligacao|WhatsApp|Presencial|Outro).");
        if (!Enum.TryParse<ResultadoContato>(request.Resultado, ignoreCase: true, out var resultado))
            throw new ValidacaoException("contato.resultado_invalido",
                "Resultado inválido (Atendeu|NaoAtendeu|CaixaPostal|NumeroInvalido|Outro).");

        // Contato é ancorado na espinha. Aceita tanto o id público do EXAME (ExameImagem) quanto
        // o id da própria SOLICITACAO (espinha) — consultas não têm ExameImagem, então o id que
        // chega já é o da espinha. Sem este fallback, registrar contato numa consulta lançava
        // NaoEncontrado (assimetria: o ObterAsync já resolvia pela espinha e este não).
        var solicitacao = await db.ExamesImagem.AsNoTracking()
            .Where(e => e.Id == solicitacaoExameId && e.ExcluidoEm == null)
            .Select(e => new { e.SolicitacaoId, e.Solicitacao!.PacienteId })
            .FirstOrDefaultAsync(ct);
        solicitacao ??= await db.Solicitacoes.AsNoTracking()
            .Where(s => s.Id == solicitacaoExameId && s.ExcluidoEm == null)
            .Select(s => new { SolicitacaoId = s.Id, s.PacienteId })
            .FirstOrDefaultAsync(ct);
        if (solicitacao is null)
            throw new NaoEncontradoException("solicitacao", solicitacaoExameId.ToString());

        var obs = request.Observacao?.Trim();
        db.ContatosRegistro.Add(new ContatoRegistro
        {
            Id = Guid.CreateVersion7(),
            SolicitacaoId = solicitacao.SolicitacaoId,
            PacienteId = solicitacao.PacienteId,
            Meio = meio,
            Resultado = resultado,
            Observacao = string.IsNullOrEmpty(obs) ? null : (obs.Length <= 500 ? obs : obs[..500]),
            CriadoEm = DateTime.UtcNow,
            CriadoPor = usuarioAtual.UsuarioId,
        });
        await db.SaveChangesAsync(ct);
    }
}
