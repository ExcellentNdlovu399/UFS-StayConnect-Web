using UFSStayConnect.Data;
using UFSStayConnect.Models;

namespace UFSStayConnect.DataAccess
{
    public class RepositoryWrapper : IRepositoryWrapper
    {
        private readonly AppDbContext _appDbContext;

        private IAccommodationRepository? _accommodation;
        private IRoomRepository? _room;
        private IServiceRepository? _service;
        private IServiceAssignmentRepository? _serviceAssignment;
        private IApplicationRepository? _application;
        private IAccommodationChargeRepository? _accommodationCharge;
        private IPaymentRepository? _payment;
        private IPaymentTypeRepository? _paymentType;
        private INotificationRepository? _notification;
        private IPasswordResetRequestRepository? _passwordResetRequest;
        public RepositoryWrapper(AppDbContext appDbContext)
        {
            _appDbContext = appDbContext;
        }

        public IAccommodationRepository Accommodation
        {
            get
            {
                if (_accommodation == null)
                {
                    _accommodation =
                        new AccommodationRepository(_appDbContext);
                }

                return _accommodation;
            }
        }

        public IRoomRepository Room
        {
            get
            {
                if (_room == null)
                    _room = new RoomRepository(_appDbContext);
                return _room;
            }
        }

        public IServiceRepository Service
        {
            get
            {
                if (_service == null)
                {
                    _service =
                        new ServiceRepository(_appDbContext);
                }

                return _service;
            }
        }

        public IServiceAssignmentRepository ServiceAssignment
        {
            get
            {
                if (_serviceAssignment == null)
                    _serviceAssignment = new ServiceAssignmentRepository(_appDbContext);
                return _serviceAssignment;
            }
        }

        public IApplicationRepository Application
        {
            get
            {
                if (_application == null)
                {
                    _application =
                        new ApplicationRepository(_appDbContext);
                }

                return _application;
            }
        }

        public IAccommodationChargeRepository AccommodationCharge
        {
            get
            {
                if (_accommodationCharge == null)
                {
                    _accommodationCharge =
                        new AccommodationChargeRepository(_appDbContext);
                }

                return _accommodationCharge;
            }
        }

        public IPaymentRepository Payment
        {
            get
            {
                if (_payment == null)
                {
                    _payment = new PaymentRepository(_appDbContext);
                }

                return _payment;
            }
        }

        public IPaymentTypeRepository PaymentType
        {
            get
            {
                if (_paymentType == null)
                {
                    _paymentType =
                        new PaymentTypeRepository(_appDbContext);
                }

                return _paymentType;
            }
        }

        public INotificationRepository Notification
        {
            get
            {
                if (_notification == null)
                    _notification = new NotificationRepository(_appDbContext);
                return _notification;
            }
        }

        public IPasswordResetRequestRepository PasswordResetRequest
        {
            get
            {
                if (_passwordResetRequest == null)
                {
                    _passwordResetRequest =
                        new PasswordResetRequestRepository(_appDbContext);
                }

                return _passwordResetRequest;
            }
        }

        public void Save()
        {
            _appDbContext.SaveChanges();
        }
    }
}