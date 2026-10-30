using UFSStayConnect.Models;

namespace UFSStayConnect.DataAccess
{
    public interface IRepositoryWrapper
    {
        IAccommodationRepository Accommodation { get; }
        IRoomRepository Room { get; }
        IServiceRepository Service { get; }
        IServiceAssignmentRepository ServiceAssignment { get; }
        IApplicationRepository Application { get; }
        IPaymentRepository Payment { get; }
        IAccommodationChargeRepository AccommodationCharge { get; }
        IPaymentTypeRepository PaymentType { get; }
        INotificationRepository Notification { get; }
        IPasswordResetRequestRepository PasswordResetRequest { get; }
        void Save();
    }
}