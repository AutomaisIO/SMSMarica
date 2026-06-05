using Microsoft.Extensions.Configuration;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Inteligencia.Seguranca;
using SMSMarica.Data.Entities.Enums;
using SMSMarica.Data.Entities.Ia;

namespace SMSMarica.Core.Inteligencia.Fontes;

/// <summary>
/// Cria a <see cref="IFonteDados"/> concreta a partir do registro <see cref="IaFonte"/>:
/// decifra a senha read-only e injeta os limites operacionais (timeout / cap de linhas)
/// lidos da configuração (<c>Ia:TimeoutSegundos</c>, <c>Ia:RowLimit</c>).
/// </summary>
public sealed class FonteDadosFactory(IProtetorSegredos protetor, IConfiguration configuration)
    : IFonteDadosFactory
{
    private readonly int _timeoutSegundos = configuration.GetValue("Ia:TimeoutSegundos", 30);
    private readonly int _rowLimit = configuration.GetValue("Ia:RowLimit", 1000);

    public IFonteDados Criar(IaFonte fonte)
    {
        ArgumentNullException.ThrowIfNull(fonte);

        return fonte.Dialeto switch
        {
            DialetoSql.Oracle => CriarOracle(fonte),
            _ => throw new NotSupportedException(
                $"Dialeto '{fonte.Dialeto}' (fonte '{fonte.Nome}') ainda não é suportado pelo módulo IA."),
        };
    }

    private SaluxOracleFonte CriarOracle(IaFonte fonte)
    {
        if (string.IsNullOrWhiteSpace(fonte.Host))
        {
            throw new ValidacaoException("ia.fonte.host", $"Fonte '{fonte.Nome}' sem host configurado.");
        }

        if (string.IsNullOrWhiteSpace(fonte.Servico))
        {
            throw new ValidacaoException("ia.fonte.servico", $"Fonte '{fonte.Nome}' sem serviço/SID configurado.");
        }

        if (string.IsNullOrWhiteSpace(fonte.Usuario))
        {
            throw new ValidacaoException("ia.fonte.usuario", $"Fonte '{fonte.Nome}' sem usuário configurado.");
        }

        if (string.IsNullOrWhiteSpace(fonte.SenhaCifrada))
        {
            throw new ValidacaoException("ia.fonte.senha", $"Fonte '{fonte.Nome}' sem senha configurada.");
        }

        var senha = protetor.Revelar(fonte.SenhaCifrada);

        return new SaluxOracleFonte(
            host: fonte.Host,
            porta: fonte.Porta ?? 1521,
            servico: fonte.Servico,
            usuario: fonte.Usuario,
            senha: senha,
            commandTimeoutSegundos: _timeoutSegundos,
            maxLinhas: _rowLimit);
    }
}
