using Microsoft.EntityFrameworkCore;
using SMSMais.Data;

namespace SMSMais.Core.Integracoes.SisregWeb;

/// <summary>
/// Chave-mestra do sincronismo automático com o SISREG, lida por todos os agendadores.
///
/// <para>Existe como ponto único porque a resposta precisa ser idêntica nos dois agendadores
/// (varredura das unidades e lote de mapeamento): se um deles interpretasse "sem configuração"
/// de outro jeito, desligar pela tela pararia metade do que consome a sessão do SISREG e o
/// operador leria isso como "o botão não funciona".</para>
/// </summary>
public static class SincronismoAutomaticoSisreg
{
    /// <summary>
    /// O sincronismo automático pode disparar? <b>Sem linha de configuração devolve
    /// <c>true</c></b> — a varredura não depende dessa linha para funcionar (a credencial do
    /// scraping vive em <c>integracao_credencial</c>), então tratar a ausência como "desligado"
    /// pararia instalação que nunca abriu a tela de configuração da API-SISREG.
    /// </summary>
    public static async Task<bool> LigadoAsync(SmsMaisDbContext db, CancellationToken ct)
    {
        var ativo = await db.SisregConfiguracoes
            .AsNoTracking()
            .Select(c => (bool?)c.SincronismoAutomaticoAtivo)
            .FirstOrDefaultAsync(ct);

        return ativo ?? true;
    }
}
