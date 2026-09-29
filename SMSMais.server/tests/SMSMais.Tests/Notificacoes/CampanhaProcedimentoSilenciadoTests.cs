using SMSMais.Core.Notificacoes.Campanhas;
using SMSMais.Data.Entities.Sisreg;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Notificacoes;

/// <summary>
/// A exceção da campanha (mutirão da Carreta, 29/09/2026): a campanha em envio automático ignora a
/// chave da unidade e cobre procedimento fora do mapeamento — mas um procedimento explicitamente
/// DESMARCADO (todas as linhas dele na unidade com o aviso desligado) é respeitado. É o que
/// permite "desmarcar só tomografia e manter o resto automático" sem desligar a campanha inteira.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class CampanhaProcedimentoSilenciadoTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Desmarcado_em_todas_as_linhas_silencia_ligado_em_uma_ou_fora_do_mapeamento_nao()
    {
        await using var db = fixture.CriarDbContext();
        var unidadeId = Guid.NewGuid();
        var outraUnidadeId = Guid.NewGuid();
        db.Unidades.Add(new SMSMais.Data.Entities.Unidade
        {
            Id = unidadeId, Nome = "UNIDADE DE TESTE (exemplo)", CriadoEm = DateTime.UtcNow,
        });

        var prof = new SisregProfissionalUnidade
        {
            Id = Guid.CreateVersion7(),
            UnidadeId = unidadeId,
            Cpf = "24156779653",
            Nome = "PROFISSIONAL DE TESTE (exemplo)",
            Habilitado = true,
            CriadoEm = DateTime.UtcNow,
        };
        db.SisregProfissionaisUnidade.Add(prof);

        // TC desmarcada (aviso desligado) e US ligada, na MESMA unidade.
        db.SisregProcedimentosProfissional.Add(new SisregProcedimentoProfissional
        {
            Id = Guid.CreateVersion7(), ProfissionalId = prof.Id,
            Codigo = "3500019", Nome = "TOMOGRAFIA COMPUTADORIZADA DO ABDOMEN TOTAL",
            Habilitado = false, EnviarConfirmacao = false,
        });
        db.SisregProcedimentosProfissional.Add(new SisregProcedimentoProfissional
        {
            Id = Guid.CreateVersion7(), ProfissionalId = prof.Id,
            Codigo = "1402147", Nome = "ULTRA-SONOGRAFIA DE MAMAS BILATERAL",
            Habilitado = true, EnviarConfirmacao = true,
        });
        await db.SaveChangesAsync();

        // Desmarcado em todas as linhas da unidade → silenciado.
        Assert.True(await CampanhaResolver.ProcedimentoSilenciadoAsync(db, unidadeId, "3500019"));

        // Com o aviso ligado em (pelo menos) uma linha → envia.
        Assert.False(await CampanhaResolver.ProcedimentoSilenciadoAsync(db, unidadeId, "1402147"));

        // Fora do mapeamento da unidade → a campanha continua cobrindo (comportamento original).
        Assert.False(await CampanhaResolver.ProcedimentoSilenciadoAsync(db, unidadeId, "9999999"));

        // Sem código não há chave a respeitar.
        Assert.False(await CampanhaResolver.ProcedimentoSilenciadoAsync(db, unidadeId, null));

        // O mapeamento é POR UNIDADE: a mesma TC desmarcada aqui não silencia outra unidade.
        Assert.False(await CampanhaResolver.ProcedimentoSilenciadoAsync(db, outraUnidadeId, "3500019"));

        // SEM código (linha da importação pontual, que o cons_agendas devolve só com o nome):
        // casa pelo NOME, tolerando os espaços duplicados que o TXT do SISREG traz.
        Assert.True(await CampanhaResolver.ProcedimentoSilenciadoAsync(
            db, unidadeId, null, "TOMOGRAFIA COMPUTADORIZADA DO ABDOMEN  TOTAL"));
        Assert.True(await CampanhaResolver.ProcedimentoSilenciadoAsync(
            db, unidadeId, null, "tomografia computadorizada do abdomen total"));
        Assert.False(await CampanhaResolver.ProcedimentoSilenciadoAsync(
            db, unidadeId, null, "ULTRA-SONOGRAFIA DE MAMAS BILATERAL"));
        Assert.False(await CampanhaResolver.ProcedimentoSilenciadoAsync(
            db, unidadeId, null, "PROCEDIMENTO QUE NAO EXISTE NO MAPEAMENTO"));
        Assert.False(await CampanhaResolver.ProcedimentoSilenciadoAsync(db, unidadeId, null, null));
    }
}
