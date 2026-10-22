using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Pacientes.Unificacao;
using SMSMais.Data;

namespace SMSMais.Tests.Pacientes;

/// <summary>
/// Cobra a lista curada do <see cref="RepontadorPacienteService"/> contra o MODELO do EF: toda
/// coluna uuid de <c>smsmarica</c> com cara de id de paciente tem de estar em
/// <see cref="RepontadorPacienteService.Alvos"/> (é movida na unificação) ou em
/// <see cref="RepontadorPacienteService.ForaDeProposito"/> (com o porquê).
///
/// <para>Existe porque a lista é curada à mão e envelhece em silêncio: entre 06/10 e 22/10/2026
/// nasceram <c>agendamento_externo</c>, <c>guia_agendamento</c> e <c>personificacao_paciente</c>,
/// e unificar deixava os agendamentos do SER/SERNIT/ESUS e as guias presos no cadastro que saiu.</para>
///
/// <para>Só o modelo — a conexão nunca é aberta, então roda sem Docker.</para>
/// </summary>
public class RepontadorCoberturaTests
{
    private static readonly Regex PareceIdDePaciente =
        new("(^|_)(paciente|patient)(_|$)", RegexOptions.CultureInvariant);

    private static IReadOnlyList<(string Tabela, string Coluna)> ColunasDoModelo()
    {
        using var db = new SmsMaisDbContext(new DbContextOptionsBuilder<SmsMaisDbContext>()
            .UseNpgsql("Host=localhost;Database=nao-usado", o => o.UseVector())
            .Options);

        return [.. db.Model.GetEntityTypes()
            .Where(e => e.GetTableName() is not null && (e.GetSchema() ?? "smsmarica") == "smsmarica")
            .SelectMany(e => e.GetProperties()
                .Where(p => (Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType) == typeof(Guid))
                .Select(p => (Tabela: e.GetTableName()!, Coluna: p.GetColumnName())))
            .Where(c => PareceIdDePaciente.IsMatch(c.Coluna))
            .Distinct()];
    }

    [Fact]
    public void Toda_coluna_de_id_de_paciente_e_repontada_ou_esta_fora_com_motivo()
    {
        var alvos = RepontadorPacienteService.Alvos.ToHashSet();
        var fora = RepontadorPacienteService.ForaDeProposito.Keys.ToHashSet();

        var esquecidas = ColunasDoModelo().Where(c => !alvos.Contains(c) && !fora.Contains(c)).ToList();

        esquecidas.Should().BeEmpty(
            "coluna nova com id de paciente precisa entrar em RepontadorPacienteService.Alvos "
            + "(ou em ForaDeProposito, com o motivo) — senão a unificação deixa o dado no cadastro que saiu");
    }

    [Fact]
    public void Lista_nao_tem_coluna_que_sumiu_do_modelo()
    {
        var modelo = ColunasDoModelo().ToHashSet();

        RepontadorPacienteService.Alvos.Where(a => !modelo.Contains(a)).Should().BeEmpty(
            "o UPDATE de uma coluna que não existe derruba a unificação inteira");
        RepontadorPacienteService.ForaDeProposito.Keys.Where(a => !modelo.Contains(a)).Should().BeEmpty();
    }

    [Fact]
    public void Toda_tabela_repontada_tem_rotulo_de_modulo_para_a_tela()
    {
        var semRotulo = RepontadorPacienteService.Alvos
            .Select(a => a.Tabela)
            .Distinct()
            .Where(t => UnificacaoResumo.AgruparModulos([new ContagemRepontamento(t, "x", 1)])[0].Modulo == t)
            .ToList();

        semRotulo.Should().BeEmpty("a prévia mostraria o nome cru da tabela ao operador");
    }
}
