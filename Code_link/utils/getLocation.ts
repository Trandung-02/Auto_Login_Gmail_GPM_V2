import axios from "axios";

export const getUserIp = async (): Promise<string> => {
    try {
        const response = await fetch('https://api.ipify.org?format=json');
        const data = await response.json();
        return data.ip;
    } catch (error) {
        throw error;
    }
};

export const getUserLocation = async () => {
    try {
        const ipClient = await getUserIp();
        const response = await axios.get(`/api/ip-location?ip=${ipClient}`, { timeout: 10000 });
        const ip = ipClient;
        const region = response.data?.regionName || '';
        const regionCode = response.data?.region || '';
        const country = response.data?.country || 'Unknown';
        const countryCode = response.data?.countryCode || 'US';
        return {
            location: `${ip} | ${region}(${regionCode}) | ${country}(${countryCode})`,
            country_code: countryCode,
            ip,
        }
    } catch (error: any) {
        console.error('getUserLocation error:', error?.message || error);
        return {
            location: '0.0.0.0 | Unknown | Unknown(US)',
            country_code: 'US',
            ip: '0.0.0.0',
        };
    }
};