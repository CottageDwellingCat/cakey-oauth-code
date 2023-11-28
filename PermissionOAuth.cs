namespace CakeyBotCore.Persistence;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("PermissionOAuth")]
public sealed class PermissionOAuth
{
	[Key]
	[Column("ID")]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public int Id { get; set; }

	[Column("UserID")]
	public ulong UserId { get; set; }

	[Column("BearerToken")]
	public string BearerToken { get; set; }

	[Column("RefreshToken")]
	public string RefreshToken { get; set; }

	[Column("ExpiresAt")]
	public double ExpiresAt { get; set; }
}
