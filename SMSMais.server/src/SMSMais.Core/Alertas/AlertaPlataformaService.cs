using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Notificacoes.Sincronismo;
using SMSMais.Core.Notificacoes.WhatsApp;
using SMSMais.Data;
using SMSMais.Data.Entities.Alertas;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Alertas;

public sealed record AlertaDestinatarioDto(Guid Id, string Telefone, string? Nome, bool Ativo, DateTime CriadoEm);

public sealed record AlertaOrigemDto(
    string Chave, string Rotulo, string Grupo, string Descricao, bool Catalogada, bool Silenciada,
    int Ocorrencias, DateTime? UltimaOcorrenciaEm, DateTime? UltimoAvisoEm, int OcorrenciasSemAviso,
    string? UltimoTitulo, string? UltimoDetalhe);

public sealed record AlertaEnvioDto(
    Guid Id, string OrigemChave, string? OrigemRotulo, string Titulo, string Detalhe, DateTime CriadoEm,
    int Ocorrencias, SituacaoAlertaEnvio Situacao, string? Resultado);

public sealed record TelefonesIntegracaoDto(string Provedor, IReadOnlyList<string> Telefones);

/// <param name="TemplateAprovado">O template aparece como APROVADO no catálogo da Meta.</param>
/// <param name="TemplateCorpo">Corpo aprovado (null enquanto não aprovado).</param>
public sealed record AlertaTemplateDto(
    string Nome, bool TemplateAprovado, string? TemplateCorpo, int? ParametrosAprovados,
    IReadOnlyList<string> ParametrosConfigurados, int TetoDiario);

public sealed record AlertaPainelDto(
    IReadOnlyList<AlertaDestinatarioDto> Destinatarios,
    IReadOnlyList<TelefonesIntegracaoDto> TelefonesPorIntegracao,
    IReadOnlyList<AlertaOrigemDto> Origens,
    IReadOnlyList<AlertaEnvioDto> Envios,
    AlertaTemplateDto Template);

public sealed record SalvarAlertaDestinatarioRequest(string Telefone, string? Nome, bool Ativo = true);

public sealed record SilenciarAlertaOrigemRequest(bool Silenciada);

public interface IAlertaPlataformaService
{
    Task<AlertaPainelDto> ObterPainelAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AlertaEnvioDto>> ListarEnviosAsync(string? origem, int limite, CancellationToken ct = default);
    Task<AlertaDestinatarioDto> AdicionarDestinatarioAsync(SalvarAlertaDestinatarioRequest req, CancellationToken ct = default);
    Task<AlertaDestinatarioDto> AtualizarDestinatarioAsync(Guid id, SalvarAlertaDestinatarioRequest req, CancellationToken ct = default);
    Task RemoverDestinatarioAsync(Guid id, CancellationToken ct = default);
    Task<AlertaOrigemDto> SilenciarAsync(string chave, bool silenciada, CancellationToken ct = default);
    Task<AlertaEnvioDto> TestarAsync(CancellationToken ct = default);
}

public sealed class AlertaPlataformaService(
    SmsMaisDbContext db,
    AlertaPlataformaDespachante despachante,
    INotificadorSincronismo sincronismo,
    IWhatsAppCliente whatsApp,
    IOptions<AlertaPlataformaOptions> opcoes,
    IUsuarioAtualAccessor usuarioAtual) : IAlertaPlataformaService
{
    private static readonly string[] ProvedoresSincronismo = ["sisreg", "ser", "sernit"];

    public async Task<AlertaPainelDto> ObterPainelAsync(CancellationToken ct = default)
    {
        var destinatarios = await db.AlertaDestinatarios.AsNoTracking()
            .OrderBy(d => d.CriadoEm)
            .Select(d => new AlertaDestinatarioDto(d.Id, d.Telefone, d.Nome, d.Ativo, d.CriadoEm))
            .ToListAsync(ct);

        var porIntegracao = new List<TelefonesIntegracaoDto>();
        foreach (var provedor in ProvedoresSincronismo)
        {
            var tels = await sincronismo.ListarTelefonesAsync(provedor, ct);
            if (tels.Count > 0) porIntegracao.Add(new TelefonesIntegracaoDto(provedor, tels));
        }

        var linhas = await db.AlertaOrigens.AsNoTracking().ToListAsync(ct);
        var porChave = linhas.ToDictionary(o => o.Chave, StringComparer.Ordinal);

        // Catálogo primeiro (mesmo o que nunca ocorreu: a tela diz o que SERIA reportado), depois
        // o que a captura do log descobriu, mais recente primeiro.
        var origens = AlertaCatalogo.Conhecidas
            .Select(c => Projetar(porChave.GetValueOrDefault(c.Chave), c))
            .Concat(linhas
                .Where(o => AlertaCatalogo.Buscar(o.Chave) is null)
                .OrderByDescending(o => o.UltimaOcorrenciaEm)
                .Select(o => Projetar(o, null)))
            .ToList();

        var envios = await ListarEnviosAsync(null, 100, ct);

        var op = opcoes.Value;
        var aprovado = (await whatsApp.ListarTemplatesAsync(ct))
            .FirstOrDefault(t => string.Equals(t.Nome, op.Template, StringComparison.OrdinalIgnoreCase));
        var template = new AlertaTemplateDto(
            op.Template, aprovado is not null, aprovado?.Corpo, aprovado?.Parametros, op.Parametros, op.TetoDiario);

        return new AlertaPainelDto(destinatarios, porIntegracao, origens, envios, template);
    }

    public async Task<IReadOnlyList<AlertaEnvioDto>> ListarEnviosAsync(
        string? origem, int limite, CancellationToken ct = default)
    {
        var q = db.AlertaEnvios.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(origem)) q = q.Where(e => e.OrigemChave == origem);

        var envios = await q.OrderByDescending(e => e.CriadoEm).Take(Math.Clamp(limite, 1, 500)).ToListAsync(ct);
        var chaves = envios.Select(e => e.OrigemChave).Distinct().ToList();
        var rotulos = await db.AlertaOrigens.AsNoTracking()
            .Where(o => chaves.Contains(o.Chave))
            .ToDictionaryAsync(o => o.Chave, o => o.Rotulo, ct);

        return [.. envios.Select(e => new AlertaEnvioDto(
            e.Id, e.OrigemChave, rotulos.GetValueOrDefault(e.OrigemChave), e.Titulo, e.Detalhe,
            e.CriadoEm, e.Ocorrencias, e.Situacao, e.Resultado))];
    }

    public async Task<AlertaDestinatarioDto> AdicionarDestinatarioAsync(
        SalvarAlertaDestinatarioRequest req, CancellationToken ct = default)
    {
        var telefone = ValidarTelefone(req.Telefone);
        if (await db.AlertaDestinatarios.AnyAsync(d => d.Telefone == telefone, ct))
            throw new ConflitoException("alerta.telefone_duplicado", "Este telefone já recebe os avisos.");

        var d = new AlertaDestinatario
        {
            Id = Guid.CreateVersion7(),
            Telefone = telefone,
            Nome = Nome(req.Nome),
            Ativo = req.Ativo,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = await QuemAsync(ct),
        };
        db.AlertaDestinatarios.Add(d);
        await db.SaveChangesAsync(ct);
        return new AlertaDestinatarioDto(d.Id, d.Telefone, d.Nome, d.Ativo, d.CriadoEm);
    }

    public async Task<AlertaDestinatarioDto> AtualizarDestinatarioAsync(
        Guid id, SalvarAlertaDestinatarioRequest req, CancellationToken ct = default)
    {
        var d = await db.AlertaDestinatarios.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NaoEncontradoException("Destinatário de aviso", id);

        var telefone = ValidarTelefone(req.Telefone);
        if (telefone != d.Telefone && await db.AlertaDestinatarios.AnyAsync(x => x.Telefone == telefone, ct))
            throw new ConflitoException("alerta.telefone_duplicado", "Este telefone já recebe os avisos.");

        d.Telefone = telefone;
        d.Nome = Nome(req.Nome);
        d.Ativo = req.Ativo;
        await db.SaveChangesAsync(ct);
        return new AlertaDestinatarioDto(d.Id, d.Telefone, d.Nome, d.Ativo, d.CriadoEm);
    }

    public async Task RemoverDestinatarioAsync(Guid id, CancellationToken ct = default)
    {
        var d = await db.AlertaDestinatarios.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NaoEncontradoException("Destinatário de aviso", id);
        db.AlertaDestinatarios.Remove(d);
        await db.SaveChangesAsync(ct);
    }

    public async Task<AlertaOrigemDto> SilenciarAsync(string chave, bool silenciada, CancellationToken ct = default)
    {
        var conhecida = AlertaCatalogo.Buscar(chave);
        var origem = await db.AlertaOrigens.FirstOrDefaultAsync(o => o.Chave == chave, ct);
        if (origem is null)
        {
            // Fonte do catálogo que ainda não ocorreu: cria a linha para guardar a escolha.
            if (conhecida is null) throw new NaoEncontradoException("Fonte de aviso", chave);
            origem = new AlertaOrigem
            {
                Chave = conhecida.Chave, Rotulo = conhecida.Rotulo, Grupo = conhecida.Grupo,
                CriadaEm = DateTime.UtcNow,
            };
            db.AlertaOrigens.Add(origem);
        }

        origem.Silenciada = silenciada;
        origem.AtualizadaPor = await QuemAsync(ct);
        await db.SaveChangesAsync(ct);
        return Projetar(origem, conhecida);
    }

    /// <summary>
    /// Manda agora, na própria requisição, e devolve o desfecho — para a tela mostrar o que a Meta
    /// respondeu (template ainda em análise, número errado) em vez de "enfileirado".
    /// </summary>
    public async Task<AlertaEnvioDto> TestarAsync(CancellationToken ct = default)
    {
        var quem = await QuemAsync(ct) ?? "operador";
        var envio = await despachante.ProcessarAsync(new EventoAlerta(
            AlertaCatalogo.Teste,
            "Teste de aviso",
            $"Disparado por {quem} na tela Avisos no celular. Se chegou, os erros da plataforma "
            + "estão chegando neste número.")
        { Forcar = true }, ct)
            ?? throw new ValidacaoException("alerta.teste", "O teste não gerou envio.");

        return new AlertaEnvioDto(
            envio.Id, envio.OrigemChave, "Mensagem de teste", envio.Titulo, envio.Detalhe, envio.CriadoEm,
            envio.Ocorrencias, envio.Situacao, envio.Resultado);
    }

    private static AlertaOrigemDto Projetar(AlertaOrigem? o, OrigemConhecida? c) => new(
        o?.Chave ?? c!.Chave,
        c?.Rotulo ?? o!.Rotulo,
        c?.Grupo ?? o!.Grupo,
        c?.Descricao ?? AlertaCatalogo.DescricaoLog,
        Catalogada: c is not null,
        Silenciada: o?.Silenciada ?? false,
        Ocorrencias: o?.Ocorrencias ?? 0,
        o?.UltimaOcorrenciaEm, o?.UltimoAvisoEm, o?.OcorrenciasSemAviso ?? 0,
        o?.UltimoTitulo, o?.UltimoDetalhe);

    private static string ValidarTelefone(string? telefone)
    {
        var d = new string([.. (telefone ?? string.Empty).Where(char.IsDigit)]);
        if (d.Length is < 10 or > 13)
            throw new ValidacaoException(
                "alerta.telefone_invalido", "Informe o telefone com DDD (10 a 13 dígitos, com ou sem o 55).");
        return d;
    }

    private static string? Nome(string? nome) =>
        string.IsNullOrWhiteSpace(nome) ? null : nome.Trim()[..Math.Min(nome.Trim().Length, 120)];

    private async Task<string?> QuemAsync(CancellationToken ct)
    {
        var id = usuarioAtual.UsuarioId;
        if (id is null) return null;
        return await db.Usuarios.AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => u.NomeCompleto)
            .FirstOrDefaultAsync(ct);
    }
}
