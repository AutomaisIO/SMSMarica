using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Inteligencia.Dtos;
using SMSMarica.Core.Inteligencia.Fontes;
using SMSMarica.Core.Inteligencia.Seguranca;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Enums;
using SMSMarica.Data.Entities.Ia;

namespace SMSMarica.Core.Inteligencia.Configuracao;

/// <summary>
/// CRUD das bases de dados (fontes) do módulo IA + teste de conexão. A senha da conta
/// read-only é cifrada em repouso e nunca devolvida (write-only; só a flag <c>SenhaDefinida</c>).
/// Exclusão é lógica (<c>ExcluidoEm</c>).
/// </summary>
public sealed class IaFonteService(
    SmsMaricaDbContext db,
    IProtetorSegredos protetor,
    IFonteDadosFactory fonteDadosFactory,
    IUsuarioAtualAccessor usuarioAtual) : IIaFonteService
{
    private readonly SmsMaricaDbContext _db = db;
    private readonly IProtetorSegredos _protetor = protetor;
    private readonly IFonteDadosFactory _fonteDadosFactory = fonteDadosFactory;
    private readonly IUsuarioAtualAccessor _usuarioAtual = usuarioAtual;

    public async Task<IReadOnlyList<FonteDetalheDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var fontes = await _db.IaFontes.AsNoTracking()
            .Where(f => f.ExcluidoEm == null)
            .OrderBy(f => f.Nome)
            .ToListAsync(cancellationToken);
        return fontes.Select(ParaDto).ToList();
    }

    public async Task<Guid> CadastrarAsync(CadastrarFonteRequest request, CancellationToken cancellationToken = default)
    {
        var fonte = new IaFonte
        {
            Id = Guid.CreateVersion7(),
            Nome = request.Nome.Trim(),
            Tipo = ParseTipo(request.Tipo),
            Dialeto = ParseDialeto(request.Dialeto),
            Ambiente = ParseAmbiente(request.Ambiente),
            Host = Normalizar(request.Host),
            Porta = request.Porta,
            Servico = Normalizar(request.Servico),
            Usuario = Normalizar(request.Usuario),
            BaseUrl = Normalizar(request.BaseUrl),
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = _usuarioAtual.UsuarioId,
        };

        if (!string.IsNullOrWhiteSpace(request.Senha))
        {
            fonte.SenhaCifrada = _protetor.Proteger(request.Senha);
        }

        _db.IaFontes.Add(fonte);
        await _db.SaveChangesAsync(cancellationToken);
        return fonte.Id;
    }

    public async Task AtualizarAsync(Guid id, AtualizarFonteRequest request, CancellationToken cancellationToken = default)
    {
        var fonte = await ObterAtivaAsync(id, cancellationToken);

        fonte.Nome = request.Nome.Trim();
        fonte.Ambiente = ParseAmbiente(request.Ambiente);
        fonte.Host = Normalizar(request.Host);
        fonte.Porta = request.Porta;
        fonte.Servico = Normalizar(request.Servico);
        fonte.Usuario = Normalizar(request.Usuario);
        fonte.BaseUrl = Normalizar(request.BaseUrl);
        fonte.Ativo = request.Ativo;

        // Senha nula/vazia = mantém a atual; preenchida = cifra e substitui.
        if (!string.IsNullOrWhiteSpace(request.Senha))
        {
            fonte.SenhaCifrada = _protetor.Proteger(request.Senha);
        }

        fonte.AtualizadoEm = DateTime.UtcNow;
        fonte.AtualizadoPor = _usuarioAtual.UsuarioId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoverAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var fonte = await ObterAtivaAsync(id, cancellationToken);

        fonte.ExcluidoEm = DateTime.UtcNow;
        fonte.ExcluidoPor = _usuarioAtual.UsuarioId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<TestarConexaoResultado> TestarConexaoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var fonte = await ObterAtivaAsync(id, cancellationToken);

        try
        {
            var ok = await _fonteDadosFactory.Criar(fonte).TestarConexaoAsync(cancellationToken);
            return new TestarConexaoResultado(ok, ok ? null : "Falha ao conectar à base.");
        }
        catch (Exception ex)
        {
            return new TestarConexaoResultado(false, ex.Message);
        }
    }

    private async Task<IaFonte> ObterAtivaAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.IaFontes.FirstOrDefaultAsync(f => f.Id == id && f.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(IaFonte), id);

    private static string? Normalizar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static FonteDetalheDto ParaDto(IaFonte f) => new(
        f.Id,
        f.Nome,
        f.Tipo.ToString(),
        f.Dialeto.ToString(),
        f.Ambiente.ToString(),
        f.Host,
        f.Porta,
        f.Servico,
        f.Usuario,
        f.BaseUrl,
        !string.IsNullOrEmpty(f.SenhaCifrada),
        f.Ativo);

    private static TipoFonte ParseTipo(string valor) =>
        Enum.TryParse<TipoFonte>(valor, ignoreCase: true, out var v)
            ? v
            : throw new ValidacaoException("iaFonte.tipo_invalido", $"Tipo de fonte inválido: '{valor}'.");

    private static DialetoSql ParseDialeto(string valor) =>
        Enum.TryParse<DialetoSql>(valor, ignoreCase: true, out var v)
            ? v
            : throw new ValidacaoException("iaFonte.dialeto_invalido", $"Dialeto SQL inválido: '{valor}'.");

    private static AmbienteFonte ParseAmbiente(string valor) =>
        Enum.TryParse<AmbienteFonte>(valor, ignoreCase: true, out var v)
            ? v
            : throw new ValidacaoException("iaFonte.ambiente_invalido", $"Ambiente inválido: '{valor}'.");
}
