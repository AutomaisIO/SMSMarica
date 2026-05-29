using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMSMarica.Data.Entities.Fhir;

namespace SMSMarica.Data.Configurations.Fhir;

internal sealed class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> b)
    {
        b.ToTable("patient", schema: "fhir");
        b.HasKey(p => p.Id);

        b.Property(p => p.Id).HasColumnName("id");
        b.Property(p => p.VersionId).HasColumnName("version_id").HasDefaultValue(1).IsRequired();
        b.Property(p => p.LastUpdated).HasColumnName("last_updated").IsRequired();
        b.Property(p => p.Active).HasColumnName("active").HasDefaultValue(true).IsRequired();
        b.Property(p => p.Gender).HasColumnName("gender").HasConversion<int>().IsRequired();
        b.Property(p => p.BirthDate).HasColumnName("birth_date");
        b.Property(p => p.BirthDateEstimated).HasColumnName("birth_date_estimated").HasDefaultValue(false).IsRequired();
        b.Property(p => p.DeceasedBoolean).HasColumnName("deceased_boolean").HasDefaultValue(false).IsRequired();
        b.Property(p => p.DeceasedDateTime).HasColumnName("deceased_datetime");
        b.Property(p => p.DeceasedPresumed).HasColumnName("deceased_presumed").HasDefaultValue(false).IsRequired();
        b.Property(p => p.MaritalStatus).HasColumnName("marital_status").HasConversion<int>().IsRequired();
        b.Property(p => p.MultipleBirthBoolean).HasColumnName("multiple_birth_boolean").HasDefaultValue(false).IsRequired();
        b.Property(p => p.MultipleBirthInteger).HasColumnName("multiple_birth_integer");
        b.Property(p => p.ManagingOrganizationId).HasColumnName("managing_organization_id");

        // Extensões BR
        b.Property(p => p.Race).HasColumnName("race").HasConversion<int>().IsRequired();
        b.Property(p => p.EtniaIndigenaCodigo).HasColumnName("etnia_indigena_codigo");
        b.Property(p => p.MothersMaidenName).HasColumnName("mothers_maiden_name").HasMaxLength(200);
        b.Property(p => p.FathersName).HasColumnName("fathers_name").HasMaxLength(200);
        b.Property(p => p.BirthCountryCode).HasColumnName("birth_country_code").HasMaxLength(3);
        b.Property(p => p.BirthMunicipioCodigo).HasColumnName("birth_municipio_codigo");
        b.Property(p => p.CountryEntryDate).HasColumnName("country_entry_date");
        b.Property(p => p.ReligiaoCodigo).HasColumnName("religiao_codigo");
        b.Property(p => p.OcupacaoCboCodigo).HasColumnName("ocupacao_cbo_codigo");
        b.Property(p => p.EducationLevel).HasColumnName("education_level").HasConversion<int>().IsRequired();
        b.Property(p => p.AttendsSchool).HasColumnName("attends_school");
        b.Property(p => p.GenderIdentity).HasColumnName("gender_identity").HasConversion<int>().IsRequired();
        b.Property(p => p.UseSocialName).HasColumnName("use_social_name").HasDefaultValue(false).IsRequired();
        b.Property(p => p.HasNoDocumentation).HasColumnName("has_no_documentation").HasDefaultValue(false).IsRequired();
        b.Property(p => p.Notes).HasColumnName("notes");

        b.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(p => p.CreatedBy).HasColumnName("created_by");
        b.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        b.Property(p => p.UpdatedBy).HasColumnName("updated_by");
        b.Property(p => p.DeletedAt).HasColumnName("deleted_at");
        b.Property(p => p.DeletedBy).HasColumnName("deleted_by");
        b.Property(p => p.LegalBasis).HasColumnName("legal_basis").HasConversion<int>().IsRequired();

        // Navegações pra lookups (FK)
        b.HasOne(p => p.ManagingOrganization)
            .WithMany()
            .HasForeignKey(p => p.ManagingOrganizationId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasOne(p => p.EtniaIndigena)
            .WithMany()
            .HasForeignKey(p => p.EtniaIndigenaCodigo)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasOne(p => p.BirthCountry)
            .WithMany()
            .HasForeignKey(p => p.BirthCountryCode)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasOne(p => p.BirthMunicipio)
            .WithMany()
            .HasForeignKey(p => p.BirthMunicipioCodigo)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasOne(p => p.Religiao)
            .WithMany()
            .HasForeignKey(p => p.ReligiaoCodigo)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasOne(p => p.OcupacaoCbo)
            .WithMany()
            .HasForeignKey(p => p.OcupacaoCboCodigo)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(p => p.DeletedAt)
            .HasDatabaseName("ix_patient_deleted_at")
            .HasFilter("deleted_at IS NULL");
    }
}
