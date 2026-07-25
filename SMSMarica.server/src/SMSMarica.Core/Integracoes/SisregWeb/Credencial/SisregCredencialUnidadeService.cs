using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Integracoes.SisregWeb.Credencial.Dtos;
using SMSMarica.Core.Inteligencia.Seguranca;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Sisreg;

namespace SMSMarica.Core.Integracoes.SisregWeb.Credencial;

/// <summary>
/// Credencial do SISREG por unidade. Senha cifrada em repouso e write-only: a tela mostra
/// apenas o usuário.
///
/// <para><b>Double-check de unidade:</b> gravar uma credencial não é só "o SISREG aceitou a
/// senha" — é também "o operador dessa senha pertence à unidade que está selecionada aqui".
/// Sem essa segunda conferência, cadastrar a senha do operador da unidade A na unidade B faria
/// o sistema mapear e varrer a agenda errada, sem nenhum sinal de erro.</para>
/// </summary>
public sealed class SisregCredencialUnidadeService(
    SmsMaricaDbContext db,
    IProtetorSegredos protetor,
    ISisregWebSessao sessao,
    ISisregUnidadeAtual unidadeAtual,
    IUsuarioAtualAccessor usuarioAtual) : ISisregCredencialUnidadeService
{
    public async Task<SisregCredencialUnidadeDto> ObterAsync(CancellationToken cancellationToken = default)
    {
        var unidade = await unidadeAtual.ObterObrigatoriaAsync(cancellationToken);
        var credencial = await BuscarAsync(unidade.Id, rastrear: false, cancellationToken);
        return ParaDto(unidade, credencial);
    }

    public async Task<SisregAutenticacaoResultadoDto> SalvarAsync(
        SalvarSisregCredencialUnidadeRequest request, CancellationToken cancellationToken = default)
    {
        var unidade = await unidadeAtual.ObterObrigatoriaAsync(cancellationToken);

        var usuario = (request.Usuario ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(request.Senha))
        {
            throw new ValidacaoException(
                "sisreg.credencial_incompleta",
                "Informe o usuário e a senha do SISREG.");
        }

        // Só grava depois que o SISREG aceitou E a unidade conferiu.
        var info = await sessao.AutenticarAvulsoAsync(usuario, request.Senha, cancellationToken);
        GarantirUnidadeConfere(unidade, info);

        var credencial = await BuscarAsync(unidade.Id, rastrear: true, cancellationToken);
        var agora = DateTime.UtcNow;

        if (credencial is null)
        {
            credencial = new SisregCredencialUnidade
            {
                Id = Guid.NewGuid(),
                UnidadeId = unidade.Id,
                CriadoEm = agora,
                CriadoPor = usuarioAtual.UsuarioId,
            };
            db.SisregCredenciaisUnidade.Add(credencial);
        }
        else
        {
            credencial.AtualizadoEm = agora;
            credencial.AtualizadoPor = usuarioAtual.UsuarioId;
        }

        credencial.Usuario = usuario;
        credencial.SenhaCifrada = protetor.Proteger(request.Senha);
        credencial.CnesConfirmado = info.Cnes;
        credencial.UnidadeSisregNome = info.UnidadeNome;
        credencial.ValidadoEm = agora;
        credencial.Ativo = true;

        await db.SaveChangesAsync(cancellationToken);

        return Sucesso(info, $"Credencial de {info.Operador} salva e validada para {unidade.Nome}.");
    }

    public async Task<SisregAutenticacaoResultadoDto> TestarAsync(CancellationToken cancellationToken = default)
    {
        var unidade = await unidadeAtual.ObterObrigatoriaAsync(cancellationToken);
        var credencial = await BuscarAsync(unidade.Id, rastrear: true, cancellationToken);

        // Sem credencial própria a sessão cai no fallback global — o teste continua valendo,
        // e o double-check é justamente o que revela que a global aponta para outra unidade.
        var info = await sessao.ObterSessaoInfoAsync(unidade.Id, cancellationToken);
        GarantirUnidadeConfere(unidade, info);

        if (credencial is not null)
        {
            credencial.CnesConfirmado = info.Cnes;
            credencial.UnidadeSisregNome = info.UnidadeNome;
            credencial.ValidadoEm = DateTime.UtcNow;
            credencial.AtualizadoEm = DateTime.UtcNow;
            credencial.AtualizadoPor = usuarioAtual.UsuarioId;
            await db.SaveChangesAsync(cancellationToken);
        }

        return Sucesso(info, $"Sessão do SISREG confirmada como {info.Operador} em {info.UnidadeNome}.");
    }

    public async Task RemoverAsync(CancellationToken cancellationToken = default)
    {
        var unidade = await unidadeAtual.ObterObrigatoriaAsync(cancellationToken);
        var credencial = await BuscarAsync(unidade.Id, rastrear: true, cancellationToken)
            ?? throw new NaoEncontradoException("Credencial SISREG da unidade", unidade.Id);

        db.SisregCredenciaisUnidade.Remove(credencial);
        await db.SaveChangesAsync(cancellationToken);
    }

    // ------------------------------------------------------------------ interno

    private Task<SisregCredencialUnidade?> BuscarAsync(Guid unidadeId, bool rastrear, CancellationToken cancellationToken)
    {
        var query = rastrear
            ? db.SisregCredenciaisUnidade.AsQueryable()
            : db.SisregCredenciaisUnidade.AsNoTracking();
        return query.FirstOrDefaultAsync(x => x.UnidadeId == unidadeId, cancellationToken);
    }

    /// <summary>
    /// Confere o CNES da unidade selecionada contra o CNES da sessão do SISREG.
    /// </summary>
    private static void GarantirUnidadeConfere(Unidade unidade, SisregSessaoInfo info)
    {
        if (string.IsNullOrWhiteSpace(unidade.Cnes))
        {
            throw new ValidacaoException(
                "sisreg.unidade_sem_cnes",
                $"A unidade '{unidade.Nome}' não tem CNES cadastrado, então não é possível conferir "
                + $"se a credencial pertence a ela. O SISREG autenticou em "
                + $"'{info.UnidadeNome}' ({info.Cnes}). Cadastre o CNES da unidade e tente de novo.");
        }

        if (string.IsNullOrWhiteSpace(info.Cnes))
        {
            throw new ValidacaoException(
                "sisreg.sessao_sem_cnes",
                "Não foi possível ler o CNES da unidade na sessão do SISREG para conferir o vínculo.");
        }

        if (!string.Equals(SoDigitos(unidade.Cnes), SoDigitos(info.Cnes), StringComparison.Ordinal))
        {
            throw new ValidacaoException(
                "sisreg.unidade_divergente",
                $"A credencial informada pertence a '{info.UnidadeNome}' (CNES {info.Cnes}), mas a "
                + $"unidade selecionada é '{unidade.Nome}' (CNES {unidade.Cnes}). Selecione a unidade "
                + $"correta no topo da tela ou use a credencial do operador desta unidade.");
        }
    }

    private static string SoDigitos(string valor) => new([.. valor.Where(char.IsDigit)]);

    private static SisregAutenticacaoResultadoDto Sucesso(SisregSessaoInfo info, string mensagem) =>
        new(true, info.Operador, info.Perfil, info.UnidadeNome, info.Cnes, true, mensagem);

    private static SisregCredencialUnidadeDto ParaDto(Unidade unidade, SisregCredencialUnidade? credencial) =>
        new(
            unidade.Id,
            unidade.Nome,
            unidade.Cnes,
            credencial?.Usuario,
            credencial is not null && !string.IsNullOrEmpty(credencial.SenhaCifrada),
            credencial?.CnesConfirmado,
            credencial?.UnidadeSisregNome,
            credencial?.ValidadoEm,
            credencial?.Ativo ?? false,
            UsandoFallbackGlobal: credencial is null || !credencial.Ativo);
}
