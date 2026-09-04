using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Data.Configurations.Sisreg;

internal sealed class SisregEscalaConfiguration : IEntityTypeConfiguration<SisregEscala>
{
    public void Configure(EntityTypeBuilder<SisregEscala> builder)
    {
        builder.ToTable("sisreg_escala");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CodigoEscala).HasColumnName("codigo_escala").HasMaxLength(20).IsRequired();

        builder.Property(x => x.UnidadeId).HasColumnName("unidade_id").IsRequired();
        builder.Property(x => x.Cnes).HasColumnName("cnes").HasMaxLength(7).IsRequired();
        builder.Property(x => x.UnidadeNomeSisreg).HasColumnName("unidade_nome_sisreg").HasMaxLength(200).IsRequired();

        builder.Property(x => x.ProfissionalCpf).HasColumnName("profissional_cpf").HasMaxLength(11).IsRequired();
        builder.Property(x => x.ProfissionalNome).HasColumnName("profissional_nome").HasMaxLength(200).IsRequired();
        builder.Property(x => x.CboCodigo).HasColumnName("cbo_codigo").HasMaxLength(10);
        builder.Property(x => x.CboDescricao).HasColumnName("cbo_descricao").HasMaxLength(200);

        builder.Property(x => x.ProcedimentoCodigo).HasColumnName("procedimento_codigo").HasMaxLength(20).IsRequired();
        builder.Property(x => x.ProcedimentoNome).HasColumnName("procedimento_nome").HasMaxLength(300).IsRequired();
        builder.Property(x => x.ProcedimentoSigtap).HasColumnName("procedimento_sigtap").HasMaxLength(20);
        builder.Property(x => x.EhGrupo).HasColumnName("eh_grupo").IsRequired();

        builder.Property(x => x.DiaSemana).HasColumnName("dia_semana").HasConversion<int>().IsRequired();
        builder.Property(x => x.HoraInicio).HasColumnName("hora_inicio").IsRequired();
        builder.Property(x => x.HoraFim).HasColumnName("hora_fim").IsRequired();
        builder.Property(x => x.VigenciaInicio).HasColumnName("vigencia_inicio").IsRequired();
        builder.Property(x => x.VigenciaFim).HasColumnName("vigencia_fim").IsRequired();

        builder.Property(x => x.VagasPrimeiraVez).HasColumnName("vagas_primeira_vez").IsRequired();
        builder.Property(x => x.MinutosPrimeiraVez).HasColumnName("minutos_primeira_vez").IsRequired();
        builder.Property(x => x.VagasRetorno).HasColumnName("vagas_retorno").IsRequired();
        builder.Property(x => x.MinutosRetorno).HasColumnName("minutos_retorno").IsRequired();
        builder.Property(x => x.VagasReserva).HasColumnName("vagas_reserva").IsRequired();
        builder.Property(x => x.MinutosReserva).HasColumnName("minutos_reserva").IsRequired();
        builder.Property(x => x.VagasTotal).HasColumnName("vagas_total").IsRequired();

        builder.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(x => x.AgendaLocal).HasColumnName("agenda_local").IsRequired();
        builder.Property(x => x.QuebraAutomatica).HasColumnName("quebra_automatica").IsRequired();

        builder.Property(x => x.OperadorCriador).HasColumnName("operador_criador").HasMaxLength(100);
        builder.Property(x => x.OperadorModificador).HasColumnName("operador_modificador").HasMaxLength(100);
        builder.Property(x => x.InseridaEmSisreg).HasColumnName("inserida_em_sisreg");
        builder.Property(x => x.AlteradaEmSisreg).HasColumnName("alterada_em_sisreg");
        builder.Property(x => x.AtivadaEmSisreg).HasColumnName("ativada_em_sisreg");

        builder.Property(x => x.VistoEm).HasColumnName("visto_em").IsRequired();
        builder.Property(x => x.Ausente).HasColumnName("ausente").IsRequired();
        builder.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(x => x.CriadoPor).HasColumnName("criado_por");
        builder.Property(x => x.AtualizadoEm).HasColumnName("atualizado_em");
        builder.Property(x => x.AtualizadoPor).HasColumnName("atualizado_por");

        builder.HasOne(x => x.Unidade)
            .WithMany()
            .HasForeignKey(x => x.UnidadeId)
            // A escala é o registro de que a oferta existiu; apagar unidade não pode levar junto o
            // que explica um agendamento antigo.
            .OnDelete(DeleteBehavior.Restrict);

        // Chave natural do SISREG: é o eixo do upsert do sincronismo. Único de verdade — no arquivo
        // real são 17.469 códigos para 17.469 linhas.
        builder.HasIndex(x => x.CodigoEscala)
            .IsUnique()
            .HasDatabaseName("ux_sisreg_escala_codigo");

        // "a oferta desta unidade neste período": o recorte de toda tela de agenda.
        builder.HasIndex(x => new { x.UnidadeId, x.Status, x.VigenciaFim });

        // "abrir a agenda deste profissional": segundo eixo de leitura, casando com
        // Solicitacao.ProfissionalExecutanteCpf.
        builder.HasIndex(x => new { x.ProfissionalCpf, x.VigenciaFim });

        // Casamento oferta × ocupação por procedimento dentro da unidade.
        builder.HasIndex(x => new { x.UnidadeId, x.ProcedimentoCodigo, x.DiaSemana });
    }
}
