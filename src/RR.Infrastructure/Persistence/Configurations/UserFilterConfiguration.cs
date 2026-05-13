using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RR.Core.Domain;

namespace RR.Infrastructure.Persistence.Configurations;

internal sealed class UserFilterConfiguration : IEntityTypeConfiguration<UserFilter>
{
    public void Configure(EntityTypeBuilder<UserFilter> builder)
    {
        builder.ToTable("user_filters");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.SemanticQuery).HasMaxLength(2000);

        builder.Property(x => x.Areas)
            .HasConversion(JsonValueConverters.StringList)
            .Metadata.SetValueComparer(JsonValueConverters.StringListComparer);

        builder.Property(x => x.PropertyTypes)
            .HasConversion(JsonValueConverters.EnumList<PropertyType>())
            .Metadata.SetValueComparer(JsonValueConverters.EnumListComparer<PropertyType>());

        builder.HasIndex(x => x.TelegramChatId);
        builder.HasIndex(x => new { x.LocationId, x.IsActive });

        builder.HasOne<Location>()
            .WithMany()
            .HasForeignKey(x => x.LocationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
