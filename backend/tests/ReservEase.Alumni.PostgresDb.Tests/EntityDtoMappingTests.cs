using ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;
using ReservEase.Alumni.PostgresDb.Sdk.Extensions;
using MemberEntity = ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni.Member;

namespace ReservEase.Alumni.PostgresDb.Tests;

public class EntityDtoMappingTests
{
    // ── Variant pricing: base + adjustment ──────────────────────────────

    [Theory]
    [InlineData(1000, 0, 1000)]
    [InlineData(1000, 1000, 2000)]
    [InlineData(1000, 1400, 2400)]
    [InlineData(1000, -200, 800)]
    [InlineData(0, 50, 50)]
    public void A_variants_price_is_the_products_base_price_plus_its_adjustment(double basePrice, double adjustment, double expected)
    {
        var product = new StoreProduct { Price = (decimal)basePrice };
        var variant = new StoreProductVariant { PriceAdjustment = (decimal)adjustment };

        var dto = variant.ToDto(product);

        Assert.Equal((decimal)expected, dto.Price);
        Assert.Equal((decimal)adjustment, dto.PriceAdjustment);
    }

    [Fact]
    public void Changing_the_base_price_moves_every_variant_with_it()
    {
        var product = new StoreProduct { Price = 1000 };
        var variants = new[] { 0m, 1000m, 1400m }.Select(a => new StoreProductVariant { PriceAdjustment = a }).ToList();
        Assert.Equal(new[] { 1000m, 2000m, 2400m }, variants.Select(v => v.ToDto(product).Price));

        product.Price = 1500;
        Assert.Equal(new[] { 1500m, 2500m, 2900m }, variants.Select(v => v.ToDto(product).Price));
    }

    [Fact]
    public void Variant_dto_carries_options_sku_stock_and_image()
    {
        var variant = new StoreProductVariant
        {
            Id = "v1", Options = new() { ["Size"] = "M", ["Color"] = "Red" }, Sku = "TEE-M-R", QuantityAvailable = 4, ImageUrl = "https://img",
        };
        var dto = variant.ToDto(new StoreProduct { Price = 10 });

        Assert.Equal("v1", dto.Id);
        Assert.Equal("M", dto.Options["Size"]);
        Assert.Equal("TEE-M-R", dto.Sku);
        Assert.Equal(4, dto.QuantityAvailable);
        Assert.Equal("https://img", dto.ImageUrl);
    }

    [Fact]
    public void Product_dto_keeps_the_base_price_and_maps_each_variant_against_it()
    {
        var product = new StoreProduct { Id = "p1", Name = "Hoodie", Price = 100, TrackStock = false, PriceLabel = "each", Stages = ["Paid", "Shipped"] };
        var dto = product.ToDto([new StoreProductVariant { PriceAdjustment = 0 }, new StoreProductVariant { PriceAdjustment = 25 }]);

        Assert.Equal(100, dto.Price);
        Assert.Equal(new[] { 100m, 125m }, dto.Variants.Select(v => v.Price));
        Assert.False(dto.TrackStock);
        Assert.Equal("each", dto.PriceLabel);
        Assert.Equal(new[] { "Paid", "Shipped" }, dto.Stages);
    }

    [Fact]
    public void Product_dto_without_variants_has_an_empty_variant_list_not_null()
        => Assert.Empty(new StoreProduct().ToDto().Variants);

    [Fact]
    public void Product_dto_maps_details_and_both_question_lists()
    {
        var product = new StoreProduct
        {
            Details = [new StoreDetailItem { Label = "Material", Value = "Cotton" }],
            Fields = [new ServiceFieldDefinition { Key = "size", Label = "Size", Type = "select", Required = true, Options = ["S", "M"], HelpText = "Pick one" }],
            DeliveryFields = [new ServiceFieldDefinition { Key = "addr", Label = "Address", Type = "text" }],
        };
        var dto = product.ToDto();

        Assert.Equal("Cotton", Assert.Single(dto.Details).Value);
        var q = Assert.Single(dto.Fields);
        Assert.Equal(("size", "Size", "select", true, "Pick one"), (q.Key, q.Label, q.Type, q.Required, q.HelpText));
        Assert.Equal(new[] { "S", "M" }, q.Options);
        Assert.Equal("addr", Assert.Single(dto.DeliveryFields).Key);
    }

    [Fact]
    public void Template_dto_maps_every_configurable_part()
    {
        var template = new StoreProductTemplate
        {
            Id = "t", Name = "Booking", Description = "d", PriceLabel = "per night", TrackStock = false, DeliveryInfo = "Pick up",
            Details = [new StoreDetailItem { Label = "L", Value = "V" }],
            Fields = [new ServiceFieldDefinition { Key = "k", Label = "K", Type = "text" }],
            DeliveryFields = [new ServiceFieldDefinition { Key = "d", Label = "D", Type = "text" }],
            Stages = ["A", "B"],
        };
        var dto = template.ToDto();

        Assert.Equal(("t", "Booking", "per night", false, "Pick up"), (dto.Id, dto.Name, dto.PriceLabel, dto.TrackStock, dto.DeliveryInfo));
        Assert.Single(dto.Details); Assert.Single(dto.Fields); Assert.Single(dto.DeliveryFields);
        Assert.Equal(new[] { "A", "B" }, dto.Stages);
    }

    // ── Orders ──────────────────────────────────────────────────────────

    [Fact]
    public void Order_dto_maps_items_answers_stages_updates_and_member_name()
    {
        var changed = new DateTime(2026, 10, 3, 9, 0, 0, DateTimeKind.Utc);
        var order = new StoreOrder
        {
            Id = "o1", OrderNumber = "ORD-1", MemberId = "m1", TotalAmount = 250, Status = "Paid", TransactionRef = "ref",
            Member = new MemberSnapshot { FirstName = "Ama", LastName = "Mensah", Email = "ama@x.com" },
            Items =
            [
                new StoreOrderItem
                {
                    ProductId = "p1", ProductName = "Hoodie", UnitPrice = 125, Quantity = 2, VariantId = "v1",
                    VariantOptions = new() { ["Size"] = "L" }, Sku = "H-L",
                    Answers = [new StoreOrderItemAnswer { Section = "details", Key = "name", Label = "Name", Type = "text", Value = "Ama" }],
                    Stages = ["Paid", "Ready"], CurrentStage = "Ready",
                    Updates = [new ServiceRequestUpdate { ChangedAt = changed, Stage = "Ready", Note = "Done", ChangedByStaffName = "Kofi" }],
                },
            ],
            DeliveryStatus = "Shipped",
            DeliveryStatusHistory = [new StoreOrderDeliveryEvent { Status = "Shipped", ChangedAt = changed }],
        };

        var dto = order.ToDto();

        Assert.Equal(("Ama Mensah", "ama@x.com"), (dto.MemberName, dto.MemberEmail));
        var item = Assert.Single(dto.Items);
        Assert.Equal((125m, 2, "v1", "H-L", "Ready"), (item.UnitPrice, item.Quantity, item.VariantId, item.Sku, item.CurrentStage));
        Assert.Equal("L", item.VariantOptions!["Size"]);
        var answer = Assert.Single(item.Answers);
        Assert.Equal(("details", "name", "Ama"), (answer.Section, answer.Key, answer.Value));
        var update = Assert.Single(item.Updates);
        Assert.Equal(("Ready", "Done", "Kofi", changed), (update.Stage, update.Note, update.ChangedByStaffName, update.ChangedAt));
        Assert.Equal("Shipped", Assert.Single(dto.DeliveryStatusHistory).Status);
        Assert.Equal(250, dto.TotalAmount);
    }

    [Fact]
    public void Order_dto_without_a_loaded_member_has_null_member_fields()
    {
        var dto = new StoreOrder { MemberId = "m1" }.ToDto();
        Assert.Null(dto.MemberName);
        Assert.Null(dto.MemberEmail);
        Assert.Equal("m1", dto.MemberId);
    }

    [Fact]
    public void Service_request_dto_maps_member_and_updates()
    {
        var request = new ServiceRequest
        {
            RequestNumber = "REQ-1", ServiceTypeName = "Transcript", Amount = 50, PaymentStatus = "Paid", CurrentStage = "Processing",
            Member = new MemberSnapshot { FirstName = "Kofi", LastName = "B", Email = "k@x.com", ProfilePictureUrl = "https://pic" },
            Updates = [new ServiceRequestUpdate { Stage = "Processing" }],
        };
        var dto = request.ToDto();

        Assert.Equal(("Kofi B", "https://pic", "Processing", 50m), (dto.MemberName, dto.MemberProfilePictureUrl, dto.CurrentStage, dto.Amount));
        Assert.Single(dto.Updates);
    }

    [Fact]
    public void Service_type_dto_maps_fields_and_stages()
    {
        var dto = new ServiceType
        {
            Name = "Transcript", Price = 30, Stages = ["Received", "Sent"],
            Fields = [new ServiceFieldDefinition { Key = "k", Label = "L", Type = "text", Required = true }],
        }.ToDto();

        Assert.Equal(("Transcript", 30m), (dto.Name, dto.Price));
        Assert.True(Assert.Single(dto.Fields).Required);
        Assert.Equal(new[] { "Received", "Sent" }, dto.Stages);
    }

    // ── Privacy: what members may see ───────────────────────────────────

    private static MemberEntity FullMember(bool email, bool phone, bool company, bool bio) => new()
    {
        Id = "m1", FirstName = "Ama", LastName = "Mensah", Email = "ama@x.com", Phone = "0241234567",
        Company = "Acme", JobTitle = "Engineer", Bio = "About me", Location = "Accra", LinkedInUrl = "https://li",
        ShowEmailOnDirectory = email, ShowPhoneOnDirectory = phone, ShowCompanyOnDirectory = company, ShowBioOnDirectory = bio,
    };

    [Fact]
    public void Directory_entries_hide_every_field_the_member_has_not_opted_to_show()
    {
        var dto = FullMember(false, false, false, false).ToDto();

        Assert.Null(dto.Email);
        Assert.Null(dto.Phone);
        Assert.Null(dto.Company);
        Assert.Null(dto.JobTitle);
        Assert.Null(dto.Bio);
        // Always-public fields remain.
        Assert.Equal(("Ama", "Mensah", "Accra"), (dto.FirstName, dto.LastName, dto.Location));
    }

    [Fact]
    public void Directory_entries_reveal_each_field_independently_when_opted_in()
    {
        Assert.Equal("ama@x.com", FullMember(true, false, false, false).ToDto().Email);
        Assert.Null(FullMember(true, false, false, false).ToDto().Phone);
        Assert.Equal("0241234567", FullMember(false, true, false, false).ToDto().Phone);
        var company = FullMember(false, false, true, false).ToDto();
        Assert.Equal(("Acme", "Engineer"), (company.Company, company.JobTitle));
        Assert.Null(company.Bio);
        Assert.Equal("About me", FullMember(false, false, false, true).ToDto().Bio);
    }

    private static MentorProfile Mentor() => new()
    {
        Area = "Engineering", MaxMentees = 3, ContactLinkedInUrl = "https://li/m", ContactWhatsAppNumber = "0551", ContactPhoneNumber = "0241",
        Member = new MemberSnapshot { FirstName = "Yaw", LastName = "A", ProfilePictureUrl = "https://p" },
    };

    private static MentorProfileSnapshot MentorSnapshot() => new()
    {
        Area = "Engineering", ContactLinkedInUrl = "https://li/m", ContactWhatsAppNumber = "0551", ContactPhoneNumber = "0241",
        Member = new MemberSnapshot { FirstName = "Yaw", LastName = "A" },
    };

    [Fact]
    public void Mentor_contact_details_are_hidden_by_default()
    {
        var dto = Mentor().ToDto();
        Assert.Null(dto.ContactLinkedInUrl);
        Assert.Null(dto.ContactWhatsAppNumber);
        Assert.Null(dto.ContactPhoneNumber);
        Assert.Equal("Yaw A", dto.MemberName);
    }

    [Fact]
    public void Mentor_contact_details_are_included_only_when_asked_for()
    {
        var dto = Mentor().ToDto(includeContact: true);
        Assert.Equal(("https://li/m", "0551", "0241"), (dto.ContactLinkedInUrl, dto.ContactWhatsAppNumber, dto.ContactPhoneNumber));
    }

    [Fact]
    public void A_mentorship_request_never_leaks_the_mentors_contact_unless_included()
    {
        var request = new MentorshipRequest { Status = "Pending", MentorProfile = MentorSnapshot(), Mentee = new MemberSnapshot { FirstName = "Esi", LastName = "O" } };

        var hidden = request.ToDto();
        Assert.Null(hidden.ContactPhoneNumber);
        Assert.Null(hidden.ContactWhatsAppNumber);
        Assert.Null(hidden.ContactLinkedInUrl);
        Assert.Equal("Esi O", hidden.MenteeName);
        Assert.Equal("Yaw A", hidden.MentorProfileName);

        var shown = request.ToDto(includeContact: true);
        Assert.Equal("0241", shown.ContactPhoneNumber);
    }

    [Fact]
    public void A_mentorship_request_without_loaded_relations_maps_to_null_names()
    {
        var dto = new MentorshipRequest { Area = "x" }.ToDto(includeContact: true);
        Assert.Null(dto.MentorProfileName);
        Assert.Null(dto.MenteeName);
        Assert.Null(dto.ContactPhoneNumber);
    }

    // ── Everything else ─────────────────────────────────────────────────

    [Fact]
    public void Job_dto_copies_every_field()
    {
        var deadline = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);
        var dto = new Job
        {
            Id = "j", CommunityId = "c", Title = "T", Company = "Co", Location = "Accra", Type = "Full-time", Description = "D",
            ApplyUrl = "https://apply", Deadline = deadline, Status = "Active", YearGroups = [2018], BannerImageUrl = "https://b",
        }.ToDto();

        Assert.Equal(("j", "c", "T", "Co", "Accra", "Full-time", "D"), (dto.Id, dto.CommunityId, dto.Title, dto.Company, dto.Location, dto.Type, dto.Description));
        Assert.Equal(("https://apply", deadline, "Active", "https://b"), (dto.ApplyUrl, dto.Deadline, dto.Status, dto.BannerImageUrl));
        Assert.Equal(new[] { 2018 }, dto.YearGroups);
    }

    [Fact]
    public void Campaign_dto_exposes_status_as_a_string_and_maps_payment_accounts()
    {
        var campaign = new Campaign
        {
            Title = "Library", TargetAmount = 1000, CollectedAmount = 250, PaidCount = 5, Status = CampaignStatus.Active, AllowManualPayments = true,
            BankAccount = new ManualPaymentBankAccount { AccountNumber = "123", AccountName = "UMaT", BankName = "GCB", Branch = "Tarkwa" },
            MobileMoneyAccount = new ManualPaymentMobileMoneyAccount { MobileMoneyNumber = "0241", Name = "UMaT", Provider = MobileMoneyProvider.MTN },
            IsMembershipCampaign = true, MembershipYear = 2026,
        };
        var dto = campaign.ToDto();

        Assert.Equal("Active", dto.Status);
        Assert.Equal(("123", "UMaT", "GCB", "Tarkwa"), (dto.BankAccount!.AccountNumber, dto.BankAccount.AccountName, dto.BankAccount.BankName, dto.BankAccount.Branch));
        Assert.Equal("MTN", dto.MobileMoneyAccount!.Provider);
        Assert.Equal((250m, 5, 2026), (dto.CollectedAmount, dto.PaidCount, dto.MembershipYear));
        Assert.True(dto.IsMembershipCampaign);
    }

    [Fact]
    public void Campaign_dto_leaves_payment_accounts_null_when_absent()
    {
        var dto = new Campaign().ToDto();
        Assert.Null(dto.BankAccount);
        Assert.Null(dto.MobileMoneyAccount);
    }

    [Fact]
    public void Business_listing_dto_maps_the_pending_edit_and_owner()
    {
        var dto = new BusinessListing
        {
            BusinessName = "Shop", Status = "Approved", HasPendingEdit = true,
            Member = new MemberSnapshot { FirstName = "A", LastName = "B", Email = "a@b.c" },
            PendingChanges = new BusinessListingPendingChanges { BusinessName = "New Shop", Location = "Kumasi" },
        }.ToDto();

        Assert.Equal(("A B", "a@b.c", "New Shop", "Kumasi"), (dto.MemberName, dto.MemberEmail, dto.PendingChanges!.BusinessName, dto.PendingChanges.Location));
        Assert.Null(new BusinessListing().ToDto().PendingChanges);
        Assert.Null(new BusinessListing().ToDto().MemberName);
    }

    [Fact]
    public void Class_note_dto_records_whether_the_viewer_liked_it()
    {
        var note = new ClassNote { Content = "Hi", LikeCount = 3, Author = new MemberSnapshot { FirstName = "A", LastName = "B" } };
        Assert.False(note.ToDto().IsLikedByMe);
        Assert.True(note.ToDto(isLikedByMe: true).IsLikedByMe);
        Assert.Equal("A B", note.ToDto().AuthorName);
        Assert.Null(new ClassNote().ToDto().AuthorName);
    }

    [Fact]
    public void Notification_preference_dtos_copy_every_toggle()
    {
        var member = new NotificationPreference
        {
            MembershipReminders = false, CampaignAlerts = false, EventReminders = true, JobAlerts = false,
            ClassNoteAlerts = true, SpotlightAlerts = false, SmsAlerts = true, WhatsAppAlerts = true, DigestFrequency = "Weekly",
        }.ToDto();
        Assert.Equal((false, false, true, false), (member.MembershipReminders, member.CampaignAlerts, member.EventReminders, member.JobAlerts));
        Assert.Equal((true, false, true, true, "Weekly"), (member.ClassNoteAlerts, member.SpotlightAlerts, member.SmsAlerts, member.WhatsAppAlerts, member.DigestFrequency));

        var admin = new AdminNotificationPreference { PaymentReceivedAlerts = false, NewMemberRegistrationAlerts = true, PendingApprovalAlerts = false, SystemAlerts = true }.ToDto();
        Assert.Equal((false, true, false, true), (admin.PaymentReceivedAlerts, admin.NewMemberRegistrationAlerts, admin.PendingApprovalAlerts, admin.SystemAlerts));
    }

    [Fact]
    public void Notification_dto_copies_routing_and_read_state()
    {
        var readAt = DateTime.UtcNow;
        var dto = new Notification
        {
            RecipientId = "m1", RecipientType = "Member", Title = "T", Body = "B", Type = "JobAlert", IsRead = true, ReadAt = readAt,
            RelatedEntityId = "j1", RelatedEntityType = "Job", ActionUrl = "/jobs/j1", ImageUrl = "https://i",
        }.ToDto();

        Assert.Equal(("m1", "Member", "JobAlert", true, readAt), (dto.RecipientId, dto.RecipientType, dto.Type, dto.IsRead, dto.ReadAt));
        Assert.Equal(("j1", "Job", "/jobs/j1"), (dto.RelatedEntityId, dto.RelatedEntityType, dto.ActionUrl));
    }
}
