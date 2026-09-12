using Microsoft.Extensions.Configuration;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Inteligencia.Seguranca;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Ia;

namespace SMSMais.Core.Inteligencia.Fontes;

/// <summary>
/// Cria a <see cref="IFonteDados"/> concreta a partir do registro <see cref="IaFonte"/>:
/// decifra a senha read-only e injeta os limites operacionais (timeout / cap de linhas)
/// lidos da configuração (<c>Ia:TimeoutSegundos</c>, <c>Ia:RowLimit</c>).
/// </summary>
public sealed class FonteDadosFactory(
    IProtetorSegredos protetor,
    IConfiguration configuration,
    Agente.IAgenteSqlRegistry agenteRegistry)
    : IFonteDadosFactory
{
    private readonly int _timeoutSegundos = configuration.GetValue("Ia:TimeoutSegundos", 30);
    private readonly int _rowLimit = configuration.GetValue("Ia:RowLimit", 1000);

    public IFonteDados Criar(IaFonte fonte)
    {
        ArgumentNullException.ThrowIfNull(fonte);

        // Base alcançada por agente proxy (WSS reverso): independe do dialeto — o agente é quem
        // fala com o banco. Ver ADR-0023.
        if (fonte.ViaAgente)
        {
            return CriarProxy(fonte);
        }

        return fonte.Dialeto switch
        {
            DialetoSql.Oracle => CriarOracle(fonte),
            DialetoSql.Postgres => CriarPostgres(fonte),
            _ => throw new NotSupportedException(
                $"Dialeto '{fonte.Dialeto}' (fonte '{fonte.Nome}') ainda não é suportado pelo módulo IA. "
                + "Para SQL Server, cadastre a base como proxy via agente (ViaAgente)."),
        };
    }

    private ProxyAgenteFonte CriarProxy(IaFonte fonte)
    {
        if (string.IsNullOrWhiteSpace(fonte.Slug))
        {
            throw new ValidacaoException(
                "ia.fonte.slug", $"Fonte proxy '{fonte.Nome}' precisa de um slug (é o id do agente).");
        }

        return new ProxyAgenteFonte(
            registry: agenteRegistry,
            agenteId: fonte.Slug,
            commandTimeoutSegundos: _timeoutSegundos,
            maxLinhas: _rowLimit);
    }

    private SaluxOracleFonte CriarOracle(IaFonte fonte)
    {
        ExigirConexaoDireta(fonte, "serviço/SID");
        var senha = protetor.Revelar(fonte.SenhaCifrada!);

        return new SaluxOracleFonte(
            host: fonte.Host!,
            porta: fonte.Porta ?? 1521,
            servico: fonte.Servico!,
            usuario: fonte.Usuario!,
            senha: senha,
            commandTimeoutSegundos: _timeoutSegundos,
            maxLinhas: _rowLimit);
    }

    /// <summary>
    /// Postgres direto. <c>Servico</c> é o nome do database. A conta cadastrada deve ser uma role
    /// só-SELECT nas tabelas daquela fonte — é ela que recorta o que a IA enxerga.
    /// </summary>
    private PostgresFonte CriarPostgres(IaFonte fonte)
    {
        ExigirConexaoDireta(fonte, "database");
        return new PostgresFonte(
            host: fonte.Host!,
            porta: fonte.Porta ?? 5432,
            database: fonte.Servico!,
            usuario: fonte.Usuario!,
            senha: protetor.Revelar(fonte.SenhaCifrada!),
            commandTimeoutSegundos: _timeoutSegundos,
            maxLinhas: _rowLimit);
    }

    private static void ExigirConexaoDireta(IaFonte fonte, string rotuloServico)
    {
        if (string.IsNullOrWhiteSpace(fonte.Host))
        {
            throw new ValidacaoException("ia.fonte.host", $"Fonte '{fonte.Nome}' sem host configurado.");
        }

        if (string.IsNullOrWhiteSpace(fonte.Servico))
        {
            throw new ValidacaoException("ia.fonte.servico", $"Fonte '{fonte.Nome}' sem {rotuloServico} configurado.");
        }

        if (string.IsNullOrWhiteSpace(fonte.Usuario))
        {
            throw new ValidacaoException("ia.fonte.usuario", $"Fonte '{fonte.Nome}' sem usuário configurado.");
        }

        if (string.IsNullOrWhiteSpace(fonte.SenhaCifrada))
        {
            throw new ValidacaoException("ia.fonte.senha", $"Fonte '{fonte.Nome}' sem senha configurada.");
        }
    }
}
