import React from 'react'
import { SITE_MAIN_MESSAGE } from '#data/site-message'
import Modal from './Modal'

interface MenuMobileProps {
    isOpend: boolean;
    onToggleAdvanced: (value: boolean) => void;
}

const MenuMobile: React.FC<MenuMobileProps> = ({ isOpend, onToggleAdvanced }) => {

    const [isOpen, setIsOpen] = React.useState(isOpend);
    const [isOpendPrivacyPolicy, setIsOpendPrivacyPolicy] = React.useState(false);
    const [isOpendOther, setIsOpendOther] = React.useState(false);

    const handleOpendPrivacyPolicy = () => {
        setIsOpendPrivacyPolicy(!isOpendPrivacyPolicy);
    }

    React.useEffect(() => {
        setIsOpen(isOpend);
    }, [isOpend]);

    const handleClose = () => {
        setIsOpen(false);
        onToggleAdvanced(false);
    };

    return (
        <Modal
            isOpen={isOpen}
            title=""
            onClose={handleClose}
        >

            <div className='w-full'>
                <p className='font-[600] text-[24px]'>{SITE_MAIN_MESSAGE}</p>

                <div className='text-[16px] max-w-full w-full mt-5'>
                    <div className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[12px] transition-all duration-200 bg-[#354855] text-white'>
                        <div className='flex items-center justify-center justify-start gap-4'>
                            <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px]'>
                                <img src="/images/icons/ic_home.svg" className='w-full h-full text-white white' alt="home" />
                            </div>
                            <span className='text-white font-[500] text-[15px]'>{SITE_MAIN_MESSAGE}</span>
                        </div>
                    </div>

                    <div className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[12px]'>
                        <div className='flex items-center justify-center justify-start gap-4'>
                            <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px]'>
                                <img src="/images/icons/ic_search.svg" className='w-full h-full' alt="search" />
                            </div>
                            <span className='text-black font-[500] text-[15px]'>{SITE_MAIN_MESSAGE}</span>
                        </div>
                    </div>

                    <div className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px]'>
                        <div className='flex items-center justify-center justify-start gap-4'>
                            <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px]'>
                                <img src="/images/icons/ic_setting.svg" className='w-full h-full' alt="other" />
                            </div>
                            <span className='text-black font-[500] text-[15px]'>{SITE_MAIN_MESSAGE}</span>
                        </div>
                    </div>

                    <div className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px]'>
                        <div className='flex items-center justify-center justify-start gap-4'>
                            <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px]'>
                                <img src="/images/icons/ic_topics.svg" className='w-full h-full' alt="other" />
                            </div>
                            <span className='text-black font-[500] text-[15px]'>{SITE_MAIN_MESSAGE}</span>
                        </div>
                    </div>

                    <div className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px]'>
                        <div className='flex items-center justify-center justify-start gap-4'>
                            <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px]'>
                                <img src="/images/icons/ic_more.svg" className='w-full h-full' alt="other" />
                            </div>
                            <span className='text-black font-[500] text-[15px]'>{SITE_MAIN_MESSAGE}</span>
                        </div>
                    </div>

                    <div>
                        <div onClick={() => setIsOpendPrivacyPolicy(!isOpendPrivacyPolicy)} className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px]'>
                            <div className='flex items-center justify-center justify-start gap-4'>
                                <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px]'>
                                    <img src="/images/icons/ic_locked.svg" className='w-full h-full' alt="locked" />
                                </div>
                                <span className='text-black font-[500] text-[15px]'>{SITE_MAIN_MESSAGE}</span>
                            </div>
                            <div className={`w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px] transition-all duration-200 ${isOpendPrivacyPolicy ? 'rotate-180' : ''}`}>
                                <img src="/images/icons/ic_arrow.svg" className='w-full h-full' alt="arrow" />
                            </div>
                        </div>

                        <div className={`w-[100%] ${isOpendPrivacyPolicy ? 'block' : 'hidden'}`}>
                            <div className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px]'>
                                <div className='flex items-center justify-center justify-start gap-4 pl-[24px] ml-3'>
                                    <span className='text-black font-[500] text-[15px]'>{SITE_MAIN_MESSAGE}</span>
                                    <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px] min-w-[24px] min-h-[24px]'></div>
                                </div>
                            </div>
                            <div className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px]'>
                                <div className='flex items-center justify-center justify-start gap-4 pl-[24px] ml-3'>
                                    <span className='text-black font-[500] text-[15px]'>{SITE_MAIN_MESSAGE}</span>
                                    <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px] min-w-[24px] min-h-[24px]'></div>
                                </div>
                            </div>
                            <div className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px]'>
                                <div className='flex items-center justify-center justify-start gap-4 pl-[24px] ml-3'>
                                    <span className='text-black font-[500] text-[15px]'>{SITE_MAIN_MESSAGE}</span>
                                    <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px] min-w-[24px] min-h-[24px]'></div>
                                </div>
                            </div>
                            <div className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px]'>
                                <div className='flex items-center justify-center justify-start gap-4 pl-[24px] ml-3'>
                                    <span className='text-black font-[500] text-[15px]'>{SITE_MAIN_MESSAGE}</span>
                                    <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px] min-w-[24px] min-h-[24px]'></div>
                                </div>
                            </div>
                            <div className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px]'>
                                <div className='flex items-center justify-center justify-start gap-4 pl-[24px] ml-3'>
                                    <span className='text-black font-[500] text-[15px]'>{SITE_MAIN_MESSAGE}</span>
                                    <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px] min-w-[24px] min-h-[24px]'></div>
                                </div>
                            </div>
                            <div className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px]'>
                                <div className='flex items-center justify-center justify-start gap-4 pl-[24px] ml-3'>
                                    <span className='text-black font-[500] text-[15px]'>{SITE_MAIN_MESSAGE}</span>
                                    <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px] min-w-[24px] min-h-[24px]'></div>
                                </div>
                            </div>
                            <div className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px]'>
                                <div className='flex items-center justify-center justify-start gap-4 pl-[24px] ml-3'>
                                    <span className='text-black font-[500] text-[15px]'>{SITE_MAIN_MESSAGE}</span>
                                    <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px] min-w-[24px] min-h-[24px]'></div>
                                </div>
                            </div>
                            <div className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px]'>
                                <div className='flex items-center justify-center justify-start gap-4 pl-[24px] ml-3'>
                                    <span className='text-black font-[500] text-[15px]'>{SITE_MAIN_MESSAGE}</span>
                                    <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px] min-w-[24px] min-h-[24px]'></div>
                                </div>
                            </div>
                            <div className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px]'>
                                <div className='flex items-center justify-center justify-start gap-4 pl-[24px] ml-3'>
                                    <span className='text-black font-[500] text-[15px]'>{SITE_MAIN_MESSAGE}</span>
                                    <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px] min-w-[24px] min-h-[24px]'></div>
                                </div>
                            </div>
                            <div className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px]'>
                                <div className='flex items-center justify-center justify-start gap-4 pl-[24px] ml-3'>
                                    <span className='text-black font-[500] text-[15px]'>{SITE_MAIN_MESSAGE}</span>
                                    <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px] min-w-[24px] min-h-[24px]'></div>
                                </div>
                            </div>
                            <div className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px]'>
                                <div className='flex items-center justify-center justify-start gap-4 pl-[24px] ml-3'>
                                    <span className='text-black font-[500] text-[15px]'>{SITE_MAIN_MESSAGE}</span>
                                    <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px] min-w-[24px] min-h-[24px]'></div>
                                </div>
                            </div>
                            <div className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px]'>
                                <div className='flex items-center justify-center justify-start gap-4 pl-[24px] ml-3'>
                                    <span className='text-black font-[500] text-[15px]'>{SITE_MAIN_MESSAGE}</span>
                                    <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px] min-w-[24px] min-h-[24px]'></div>
                                </div>
                            </div>
                            <div className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px]'>
                                <div className='flex items-center justify-center justify-start gap-4 pl-[24px] ml-3'>
                                    <span className='text-black font-[500] text-[15px]'>{SITE_MAIN_MESSAGE}</span>
                                    <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px] min-w-[24px] min-h-[24px]'></div>
                                </div>
                            </div>
                        </div>
                    </div>

                    <div>
                        <div onClick={() => setIsOpendOther(!isOpendOther)} className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px]'>
                            <div className='flex items-center justify-center justify-start gap-4'>
                                <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px]'>
                                    <img src="/images/icons/ic_other.svg" className='w-full h-full' alt="other" />
                                </div>
                                <span className='text-black font-[500] text-[15px]'>{SITE_MAIN_MESSAGE}</span>
                            </div>
                            <div className={`w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px] transition-all duration-200 ${isOpendOther ? 'rotate-180' : ''}`}>
                                <img src="/images/icons/ic_arrow.svg" className='w-full h-full' alt="arrow" />
                            </div>
                        </div>

                        <div className={`w-[100%] ${isOpendOther ? 'block' : 'hidden'}`}>
                            <div className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px]'>
                                <div className='flex items-center justify-between gap-3 pl-[24px] ml-3 w-full'>
                                    <span className='text-black font-[500] text-[15px]'>{SITE_MAIN_MESSAGE}</span>
                                    <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px] min-w-[24px] min-h-[24px]'></div>
                                </div>
                            </div>
                            <div className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px]'>
                                <div className='flex items-center justify-between gap-3 pl-[24px] ml-3 w-full'>
                                    <span className='text-black font-[500] text-[15px]'>{SITE_MAIN_MESSAGE}</span>
                                    <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px] min-w-[24px] min-h-[24px]'>
                                        <img src="/images/icons/ic_opend.svg" className='w-full h-full' alt="opend" />
                                    </div>
                                </div>
                            </div>
                            <div className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px]'>
                                <div className='flex items-center justify-between gap-3 pl-[24px] ml-3 w-full'>
                                    <span className='text-black font-[500] text-[15px]'>{SITE_MAIN_MESSAGE}</span>
                                    <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px] min-w-[24px] min-h-[24px]'></div>
                                </div>
                            </div>
                            <div className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px]'>
                                <div className='flex items-center justify-between gap-3 pl-[24px] ml-3 w-full'>
                                    <span className='text-black font-[500] text-[15px]'>{SITE_MAIN_MESSAGE}</span>
                                    <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px] min-w-[24px] min-h-[24px]'></div>
                                </div>
                            </div>
                            <div className='cursor-pointer flex items-center justify-between gap-0 px-4 py-3 rounded-[16px]'>
                                <div className='flex items-center justify-between gap-3 pl-[24px] ml-3 w-full'>
                                    <span className='text-black font-[500] text-[15px]'>{SITE_MAIN_MESSAGE}</span>
                                    <div className='w-[24px] h-[24px] min-w-[24px] min-h-[24px] max-w-[24px] max-h-[24px] min-w-[24px] min-h-[24px]'>
                                        <img src="/images/icons/ic_opend.svg" className='w-full h-full' alt="opend" />
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </Modal>
    );
};

export default MenuMobile;
